# 会社環境互換性チェックリスト

> **注記（2026-11-08）:** GitHub Actions（Runner、Environment承認、Release画面からの取得）は使用しない方針で、リポジトリ設定で無効化済み。成果物は開発PCの `scripts/release-local.ps1` で作成し、`RELEASE_RECORD.md` のSHA-256をSharePointの承認記録と照合してから配置する（`README.md` の「ローカル検証・リリース成果物」を参照）。以下のGitHub Actions / Runner / Environmentに関する項目は、後続のPRでローカル手順に置き換えるまで参考情報であり、実施不要。


## .NET / IIS

- [ ] .NET 10 LTSが会社標準として承認済みである
- [ ] `global.json`のSDK feature bandと会社install版のroll-forwardを確認した
- [ ] ASP.NET Core Hosting Bundleのsupport/patch/IIS moduleを確認した
- [ ] Windows Server/IISが.NET 10 support matrix内である
- [ ] .NET版変更時に`Directory.Build.props`、NuGet major、test、Migrationを一括検証した
- [ ] Productionでdeveloper exceptionを表示せずHTTPS/HSTSが有効である

## SQL Server

- [ ] SQL Server version/editionがEF Core 10 providerのsupport対象である
- [ ] encryption/TLS/certificate、collation、timezone、HA、connection resiliencyを確認した
- [ ] MigrationをStaging相当DBで適用し、index/check/FK/Identityを検証した
- [ ] SQLiteはDevelopment/Testだけで、Production起動拒否を確認した
- [ ] SQL Server実機のbackup/restoreとschema rollback方針を確認した

開発時にはSQL Server 16.x Developer Editionへの到達性、全Migrations適用、Production readinessを架空DBで検証する。機器名・DB名・connection stringはRepositoryへ記録しない。

## GitHub Actions / Runner

- [ ] `actions/checkout@v6`、`setup-dotnet@v5`、`upload-artifact@v7`の会社利用を承認した
- [ ] Self-hosted RunnerがNode.js 24 actionに必要なversion（2.327.1以上を目安）である
- [ ] Runner OS/account/network/proxy/antivirus/patch/作業folderを確認した
- [ ] GitHub.com/GHES差異とartifact action対応versionを確認した
- [ ] Environment approval、Secrets、Variables、branch protectionを確認した

## Browser / UI / network

- [ ] 会社標準browserでlogin/form/table/mobile幅/keyboard操作を確認した
- [ ] internal URL、proxy、DNS、firewall、certificate chainからHealth/launchを確認した
- [ ] Health Checkの対象host allowlist要否をsecurity担当と決めた
- [ ] Bootstrap/jQuery licenseとvulnerabilityを確認した

## 観測・運用

- [ ] Console/file logを会社collectorへ接続できる
- [ ] log folder ACL/rotation/retention/個人情報方針を確認した
- [ ] audit retention/検索/backupを確認した
- [ ] mail/Teams/監視製品への通知方法を決めた
