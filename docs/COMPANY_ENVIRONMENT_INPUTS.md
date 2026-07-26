# 会社環境で入力する値

実値をこの文書へ追記してcommitしない。値は会社のconfiguration/secret管理systemで管理する。

| key | 種別 | 用途 | 保管先候補 |
|---|---|---|---|
| `Database__Provider` | 非秘密 | Productionは`SqlServer`固定 | IIS Environment |
| `ConnectionStrings__DefaultConnection` | 秘密 | SQL Server接続 | secret store/IIS protected config |
| `BootstrapAdmin__Email` | 準秘密 | 初回admin | 一時secret |
| `BootstrapAdmin__Password` | 秘密 | 初回admin password | 一時secret、初期化後削除 |
| `DeploymentApi__Token` | 秘密 | Deployment登録API | GitHub Environment Secret + IIS secret |
| `HealthChecks__TimeoutSeconds` | 非秘密 | manual Health timeout | app config |
| `DemoData__Enabled` | 非秘密 | Productionはfalse | app config |
| `IIS_RUNNER_LABEL` | 非秘密 | Self-hosted Runner label | GitHub Variable |
| `IIS_SITE_NAME` | 非秘密/社内限定 | IIS site | GitHub Environment Variable |
| `IIS_APP_POOL_NAME` | 非秘密/社内限定 | app pool | GitHub Environment Variable |
| `IIS_DEPLOY_PATH` | 非秘密/社内限定 | deploy folder | GitHub Environment Variable |
| `IIS_BACKUP_PATH` | 非秘密/社内限定 | backup folder | GitHub Environment Variable |
| `PORTAL_BASE_URL` | 非秘密/社内限定 | Smoke Test | GitHub Environment Variable |
| `PORTAL_DEPLOYMENT_API_URL` | 非秘密/社内限定 | Deployment history API | GitHub Environment Variable |
| `PORTAL_APPLICATION_ASSET_ID` | 非秘密 | portal自身のApp ID | GitHub Environment Variable |
| `PORTAL_DEPLOYMENT_API_TOKEN` | 秘密 | API Bearer token | GitHub Environment Secret |
| `ENABLE_STAGING_DEPLOY` | 非秘密 | staging enable switch | GitHub Variable |

別途、Production URL/DNS、certificate、service account、folder ACL、GitHub Organization/team/Environment reviewer、Runner host/proxy、notification、backup/log/monitoring/retention、会社承認済み.NET patch、SQL Server DB/collationを決定する。

