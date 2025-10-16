using System.Net.Http.Headers;
using System.Text.Json;

Console.WriteLine("argment:GitBucketExportJsonFile, RepositoryPath(Owner/Repo), GitHubToken, offset, issueNum");

string jsonFile = args[0];
string repoPath = args[1];
string token = args[2];
if (args.Length < 3)
{
    Console.WriteLine("Not enough arguments");
    Console.WriteLine("e.g. hoge.json Owner/Repo hoghogeToken, 0, 100");
    Console.WriteLine("e.g. hoge.json Owner/Repo hoghogeToken, 100, 100");
    return;
}
int offset = args.Length > 3 ? int.Parse(args[3]) : 0;
int num = args.Length > 4 ? int.Parse(args[4]) : 100;

string gitHubApiUrlBase = $"https://api.github.com/repos/{repoPath}/";

using var client = new HttpClient();
client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MyApp", "1.0"));
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", token);

//jsonファイルを読み込む
var json = File.ReadAllText(jsonFile);
var issues = JsonSerializer.Deserialize<List<GitBucketIssue>>(json)!.OrderBy(i => i.number);

//GitHubにすべてIssueとして登録する(PullRequestについてもIssueとして記録)
foreach (var issue in issues.Skip(offset).Take(num))
{
    //すでに同じタイトルのIssueがある場合はスキップするなどの処理を入れる
    var checkRequest = new HttpRequestMessage(HttpMethod.Get, $"{gitHubApiUrlBase}issues/{issue.number}");
    checkRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    checkRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

    var checkResponse = await client.SendAsync(checkRequest);
    if (checkResponse.IsSuccessStatusCode)
    {
        var checkContent = await checkResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(checkContent);
        var existingTitle = doc.RootElement.GetProperty("title").GetString();
        Console.WriteLine($"Issue #{issue.number} already exist: {existingTitle}");
        break;
    }

    //登録しているIssueの前のIssueが存在しない場合は、処理を中断する
    if (issue.number > 1)
    {
        var prevRequest = new HttpRequestMessage(HttpMethod.Get, $"{gitHubApiUrlBase}issues/{issue.number - 1}");
        prevRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        prevRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        var prevResponse = await client.SendAsync(prevRequest);
        if (!prevResponse.IsSuccessStatusCode)
        {
            Console.WriteLine($"Previous issue #{issue.number - 1} does not exist. Stopping import.");
            break;
        }
    }


    //https://docs.github.com/ja/rest/issues/issues?apiVersion=2022-11-28#create-an-issue

    var title = "[FromGitBucket] " + issue.title;
    var body = $"Created by:{issue.user.login}\r\nCreated at:{issue.created_at.ToLocalTime()} (JST)\r\n\r\n" + issue.body;
    if (issue.merged is not null)
    {
        //PullRequestの場合はラベルをつける
        title = $"[FromGitBucket][PullRequest]{(issue.merged is true ? "[Merged]" : "")}" + " " + issue.title;
        body = $"Request: {issue.head?.@ref} to {issue.@base?.@ref}\r\n"
            + $"Ref: {issue.head?.sha} to {issue.@base?.sha}\r\n"
            + $"Created by: {issue.user.login}\r\n Created at:{issue.created_at.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss zzz")} (JST)\r\n"
            + $"{(issue.merged is true ? $"Merged at: {issue.merged_at!.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss zzz")} (JST)\r\nMerged by: {issue.merged_by.login}" : "Not merged")}\r\n\r\n"
            + issue.body;
    }

    // Issue作成用のJSONボディ
    var issueBody = new
    {
        title = title,
        body = body,
    };
    var jsonContent = new StringContent(JsonSerializer.Serialize(issueBody));
    jsonContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

    var response = await client.PostAsync($"{gitHubApiUrlBase}issues", jsonContent);
    if (!response.IsSuccessStatusCode)
    {
        var errorContent = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Failed to create issue: {response.StatusCode}");
        Console.WriteLine($"Response body: {errorContent}");
        break;
    }

    //https://docs.github.com/ja/rest/issues/comments?apiVersion=2022-11-28#create-an-issue-comment
    foreach (var comment in issue.comments)
    {
        var commentBody = new
        {
            body = $"Created by: {comment.user.login}\r\nCreated at: {comment.created_at.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss zzz")} (JST)\r\n\r\n" + comment.body
        };
        var commentContent = new StringContent(JsonSerializer.Serialize(commentBody));
        commentContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var res = await client.PostAsync($"{gitHubApiUrlBase}issues/{issue.number}/comments", commentContent);
        if (!res.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to post comment to issue #{issue.number}: {response.StatusCode}");
            Console.WriteLine($"Response body: {errorContent}");
            break;
        }
    }

    //https://docs.github.com/ja/rest/issues/issues?apiVersion=2022-11-28#update-an-issue
    if (issue.state is "closed")
    {
        //ToDo:だれが、いつCloseしたかをコメントに入れたいが、GitBucketのデータにそれがなさそう？？

        var closeBody = new
        {
            state = "closed"
        };
        var closedContent = new StringContent(JsonSerializer.Serialize(closeBody));
        closedContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"{gitHubApiUrlBase}issues/{issue.number}")
        {
            Content = closedContent
        };

        var res = await client.SendAsync(patchRequest);
        if (!res.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to update issue #{issue.number}: {res.StatusCode}");
            Console.WriteLine($"Response body: {errorContent}");
            break;
        }
    }
    Console.WriteLine($"Imported issue #{issue.number} - {issue.title} with {issue.comments.Count} comments.");
}

Console.WriteLine($"Import ended.");
Console.ReadLine();

//GitBucketJson
record GitBucketIssue(int number, string title, string body, GitBucketUser user, DateTimeOffset created_at, List<GitBucketComment> comments, string state, bool? merged, DateTimeOffset? merged_at, GitBucketUser merged_by, GitBucketMerge? head, GitBucketMerge? @base);
record GitBucketComment(string body, GitBucketUser user, DateTimeOffset created_at);
record GitBucketUser(string login);
record GitBucketMerge(string label, string @ref, string sha, GitBucketUser user);