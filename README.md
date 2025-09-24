[![.NET](https://github.com/kou-hon/ImportIssuesToGitHub/actions/workflows/dotnet.yml/badge.svg)](https://github.com/kou-hon/ImportIssuesToGitHub/actions/workflows/dotnet.yml)
[![.NET Build and Release](https://github.com/kou-hon/ImportIssuesToGitHub/actions/workflows/BuildAndRelease.yml/badge.svg)](https://github.com/kou-hon/ImportIssuesToGitHub/actions/workflows/BuildAndRelease.yml)

# ImportIssuesToGitHub
JSONデータからIssue/PullRequestをインポート

## 事前準備

リポジトリアクセス可能なGitHubのTokenを取得しておくこと

## 注意点

GitHubにはAPIレート制限が存在する  
これに引っかかった場合、APIでリポジトリアクセスできない（Privateの場合、NotFoundなどのレスポンスとなる）  
このため、一度に大量のissue/PullRequestをインポートするのではなく、小分けにしてインポートすること  

30分ごとに50程度のインポートであれば問題なさそう

https://docs.github.com/ja/apps/creating-github-apps/registering-a-github-app/rate-limits-for-github-apps

## 使い方

```
$ ImportIssuesToGitHub.exe C:\OWNER_Repo_issues_250918071514.json GitHubOwner/GitHubRepo ghp_hogehogetoken 100 50
```

Exportした'C:\OWNER_Repo_issues_250918071514.json'の内容を`https://github.com/GitHubOwner/GitHubRepo/`に登録する例  
issue番号101から50個を登録する

