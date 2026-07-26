# DX共通アプリ基盤

Windows Server、IIS、SQL Serverを正式環境とし、社内のアプリ、データ資産、Agent、ツール/APIを一元管理する共通入口・資産台帳です。各資産を実行する巨大な統合ランタイムではなく、所在、管理責任、権限、稼働、デプロイ、監査を可視化する保守可能なMVPです。

すべてのSeedは `Shipping Board`、`Quality Dashboard`、`Sample SQL Server`、`Production Support Agent` 等の架空データです。会社固有情報や秘密情報は含めません。

## 技術構成

- .NET 10 LTS / ASP.NET Core MVC / Razor Views / C#
- Entity Framework Core Code First
- ASP.NET Core Identity / Role / Policy Authorization
- SQL Server（Production正式DB）
- SQLite（Development/Test専用）
- Bootstrapによるレスポンシブな日本語UI
- GitHub Actions / Windows Self-hosted Runner / IIS

```text
src/DxPlatform.Web             MVC、Razor、認証・HTTP境界
src/DxPlatform.Application     権限・状態遷移・サービス契約
src/DxPlatform.Domain          Entity、列挙型、ドメイン規則
src/DxPlatform.Infrastructure  EF Core、Identity、監査、Health、Seed
tests/                         Unit / Integration Test
scripts/                       IIS deploy / rollback / smoke test
docs/                          要件、設計、運用、会社引継ぎ
```

## 必要ソフトウェア

- .NET 10 SDK（`global.json`に10.0.302、feature band roll-forwardを指定）
- ローカル起動は開発用HTTPS証明書を推奨
- SQL Server確認時のみ会社で承認されたsupport対象versionとDB作成権限（開発検証は16.x）
- IIS配置時はASP.NET Core Hosting Bundle、IIS管理機能、PowerShell `WebAdministration`

開発端末にsystem-wide SDKを導入できない場合は、公式install scriptでRepository直下の `.dotnet` へ隔離導入できます（`.dotnet` はGit対象外）。その場合は `dotnet` を `.\.dotnet\dotnet.exe` に読み替えます。

## ローカル起動

リポジトリルートで、初期管理者を環境変数に設定します。値はソースや履歴へ書かず、自分で安全な値を作成してください。

```powershell
$env:BootstrapAdmin__Email = '<local-admin-email>'
$env:BootstrapAdmin__Password = '<unique-local-password>'
dotnet restore DxPlatform.slnx
dotnet run --project src/DxPlatform.Web/DxPlatform.Web.csproj --launch-profile https
```

起動ログに表示されたHTTPS URLの `/login` を開きます。初期管理者値が両方ないDevelopmentでは管理者を作らず警告します。片方だけの場合は設定ミスとして起動を停止します。

## Development設定とDB初期化

`appsettings.Development.json`はSQLiteと架空Seedを有効にします。初回起動時に `dxplatform.dev.db` を作成し、Identityロールとデモ資産を投入します。DBを作り直す場合はアプリ停止後にローカルDBファイルを削除して再起動します。SQLiteはProductionでは明示的に拒否されます。

SQL Server用Migrationsは `src/DxPlatform.Infrastructure/Persistence/Migrations` にあります。会社またはローカル検証DBへ適用する場合は、接続文字列を環境変数または安全な管理ツールから渡します。

```powershell
dotnet tool restore
dotnet ef database update `
  --project src/DxPlatform.Infrastructure `
  --startup-project src/DxPlatform.Web `
  --connection $env:DXPLATFORM_SQLSERVER_CONNECTION
```

Productionでは次のように環境変数を使用します。実値はGitHub Secrets、IIS環境変数、承認済み秘密ストアのいずれかに置きます。

```text
Database__Provider=SqlServer
ConnectionStrings__DefaultConnection=<secret>
BootstrapAdmin__Email=<secret/config>
BootstrapAdmin__Password=<secret>
DeploymentApi__Token=<secret>
```

設定優先順位は環境変数、User Secrets、ローカル設定です。`appsettings.Production.json`はGit管理対象外です。

## テスト・ビルド・Publish

```powershell
dotnet restore DxPlatform.slnx
dotnet build DxPlatform.slnx --no-restore
dotnet test DxPlatform.slnx --no-build
dotnet publish src/DxPlatform.Web/DxPlatform.Web.csproj -c Release --no-restore -o artifacts/publish
```

Unit Testは権限競合、ロール、無効ユーザー、URL、状態遷移、必須項目を検証します。Integration Testは実際のIdentity Cookieと一時SQLite DBを使い、ログイン、管理認可、グループ権限、直接URL拒否、主要カタログCRUD、監査、Health、Deployment APIを検証します。

## 主な機能と画面

- `/dashboard`: KPI、警告、直近のデプロイと管理操作
- `/apps`: 利用権限で絞り込まれたアプリ一覧・詳細・起動
- `/data-assets`: データ資産とアプリ/Agent/データ間の関連
- `/agents`: Agentの管理責任、リスク、評価、データ/ツール関連
- `/admin/users`, `/admin/groups`, `/admin/permissions`: Identity、ロール、所属、アプリ権限
- `/admin/health-checks`: 30秒クールダウン付き手動HTTP Health Check
- `/deployments`, `POST /api/deployments`: 履歴表示とBearer token必須登録API
- `/audit-logs`, `/settings`: 安全な監査要約と秘密値を隠した設定状態
- `/health/live`, `/health/ready`: 基盤自身のHealth endpoint

## GitHub Actions

- `ci.yml`: Restore/audit、Release Build、Unit/Integration Test、Migration差分検査、会社用ZIP、TRX/artifact保存。
- `release.yml`: tagを再検証し、checksum・Migration SQL・依存関係一覧付きGitHub prereleaseを公開。
- `deploy-staging.yml`: `ENABLE_STAGING_DEPLOY=true`かつWindows Self-hosted Runner設定時だけ実行。
- `deploy-production.yml`: `workflow_dispatch`とGitHub `production` Environment承認を前提に手動実行。
- deploy scriptは配置前バックアップ、IIS停止/開始、Smoke Test、失敗時rollbackを行います。

会社RunnerやVariables/Secretsがない状態ではStaging jobはskipされ、Productionは手動開始されません。現行ActionsはNode.js 24対応Runnerを必要とするため、Runner互換性を会社チェックリストで確認します。

## 会社で差し替える箇所

SQL Server接続、URL、IIS site/app pool/配置・backup path、SSL証明書、GitHub Organization/Environment/Runner/Secrets、通知、実データ、ユーザー/権限、会社承認済み.NET版を差し替えます。downloadから構築までの順序は `docs/COMPANY_SETUP_GUIDE.md`、判定表は `docs/HANDOFF_TO_COMPANY.md`、入力値一覧は `docs/COMPANY_ENVIRONMENT_INPUTS.md`を参照してください。

会社持込み用packageは次で再生成できます。生成物は `artifacts/company/release` に出力され、Git管理されません。

```powershell
.\scripts\build-company-package.ps1 -Version 0.1.0-rc1 -DotNetPath .\.dotnet\dotnet.exe
```

## セキュリティ上の注意

- 初期管理者、接続文字列、Deployment tokenをファイルへ保存しないでください。
- 外部URLはHTTP/HTTPSだけを許可し、アプリ詳細・起動時に権限を再判定します。Health CheckはHost/Port/IP allowlist、接続時DNS再検査、redirect拒否を追加しています。
- `None`の個人権限はグループ許可より優先します。
- Audit/File logへパスワード、Token、Cookie、接続文字列、入力全文を記録しません。
- 本物のSystem Prompt、API key、認証情報、任意scriptは登録・実行できません。
- ProductionはHTTPS/HSTS、Secure/HttpOnly cookie、SQL Server、具体的な`AllowedHosts`を必須とし、DB Migrationを自動適用しません。

詳細は `docs/SECURITY.md`、既知制限は `docs/KNOWN_LIMITATIONS.md`を参照してください。
