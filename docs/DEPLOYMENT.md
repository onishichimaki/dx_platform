# 配置・CI/CD

## CI

`ci.yml`はpush/PRでWindows hosted Runnerを使い、Restore、Release Build、Unit/Integration Test、Publish、ZIP化、TRXとpublish artifact保存を行う。外部packageの既知脆弱性警告を確認し、新規code warningを0にする。

## SQL Server

1. 会社DBAがDBと最小権限user/service accountを作る。
2. `Database__Provider=SqlServer`と`ConnectionStrings__DefaultConnection`を安全な環境へ設定。
3. backupを確認後、承認済みreleaseのMigrationを適用。
4. schema/index/constraint、rollback/restore手順をDBAと確認。

```powershell
dotnet tool restore
dotnet ef migrations script --idempotent `
  --project src/DxPlatform.Infrastructure `
  --startup-project src/DxPlatform.Web `
  --output artifacts/dxplatform-migrations.sql
```

review済みscript適用または`dotnet ef database update --connection $env:...`を使う。connection stringをshell historyやActions logへechoしない。

## IIS初期構成

- Hosting Bundleをinstallし、No Managed Codeのapp poolとIIS siteを作る。
- service accountにdeploy folder read/execute、log folder modify、必要なcertificate private key、SQL最小権限を与える。
- HTTPS bindingとcertificateを設定し、HTTPはHTTPSへredirectする。
- app poolの環境変数または承認済みconfig storeへProduction設定を登録する。
- folder ACL、request size、timeout、process identity、recycle、logを会社基準で確認する。

## Staging

`deploy-staging.yml`はmain pushまたは手動開始で評価され、Repository Variable `ENABLE_STAGING_DEPLOY`が`true`の場合だけjobが動く。Windows Self-hosted Runner label、IIS/URL/API設定がない自宅Repositoryではskipされる。

## Production

`deploy-production.yml`は`workflow_dispatch`だけで開始する。GitHub `production` Environmentへrequired reviewerとbranch ruleを設定し、承認後にSecrets/Variablesを渡す。自動実行はしない。

## Variables / Secrets

Variables: `IIS_RUNNER_LABEL`、`IIS_SITE_NAME`、`IIS_APP_POOL_NAME`、`IIS_DEPLOY_PATH`、`IIS_BACKUP_PATH`、`PORTAL_BASE_URL`、`PORTAL_DEPLOYMENT_API_URL`、`PORTAL_APPLICATION_ASSET_ID`、`ENABLE_STAGING_DEPLOY`。

Secrets: `PORTAL_DEPLOYMENT_API_TOKEN`、Production connection、Bootstrap password、certificate関連。RunnerがNode.js 24 actionを実行できるversionであることを確認する。

## deploy sequence

1. source commitをcheckoutし、restore/build/test/publish。
2. publish folderをZIP化。
3. 既存配置をtimestamp folderへbackup。
4. IIS site/app pool停止、新file配置、開始。
5. `/health/ready` Smoke Test。
6. success/failureをtoken必須Deployment APIへ登録。
7. failure時は直前file backupへrollbackしjobを失敗扱いにする。

scriptはdrive root等の広すぎるpathを拒否する。初回配置でbackupがない場合のSmoke failureは自動復元元がないため、IISを停止して原因を解消する。
