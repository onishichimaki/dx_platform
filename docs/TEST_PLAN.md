# テスト計画

## 自動テスト

### Unit

- permission conflict: user deny、user grant、group max、unset deny、SystemAdmin。
- View/Use/Manage包含とApp表示可否。
- role判定、無効/lockout user判定。
- URL scheme/userinfo/relative URL拒否。
- lifecycle transition。
- Entity必須項目。

### Integration

- anonymous routeのlogin redirect。
- BootstrapAdmin login。
- Viewerのadmin拒否、SystemAdmin成功。
- group View permissionによるApp一覧表示。
- permissionなしAppの直接URL拒否。
- App/Data/Agentの登録・更新・無効化とAudit登録。
- manual Health result保存（外部networkをstub）。
- `/health/live`、`/health/ready`。
- Deployment APIのunauthorized拒否、Bearer token成功、履歴/audit保存。
- AppOwnerのHealth対象・履歴・直接POSTが所有/Manage Appに限定されること。
- ViewerのDashboard/Deployment履歴に未許可App名が表示されないこと。
- CSP、frame/MIME/referrer等のsecurity response header。
- Deployment APIのburstが429で制限されること。
- Health targetのHost/Port/IP policy（loopback、metadata、private opt-in）。

Integration Testは一時SQLite file、実Identity cookie、anti-forgery tokenを使う。test password/tokenは明示的なtest-only値で、本物のsecretではない。外部HTTP、会社system、SQL Server実DBへ依存しない。

## 手動/UAT

- mobile幅、keyboard focus、色以外のstatus表現、日本語error、confirmation画面。
- user/group/role/permissionの業務承認flow。
- App ownerとManage permissionの境界。
- unsafe URL、disabled user、lockout、concurrency conflict。
- IIS/SQL Server、certificate、folder ACL、backup/rollback、runner、Environment approval。
- Company demo削除後の実data、検索性能、監査retention、security review。

## 完了command

```powershell
dotnet restore DxPlatform.slnx
dotnet build DxPlatform.slnx --no-restore
dotnet test DxPlatform.slnx --no-build --logger "trx"
dotnet ef migrations has-pending-model-changes --project src/DxPlatform.Infrastructure --startup-project src/DxPlatform.Web --configuration Release --no-build
.\scripts\build-company-package.ps1 -Version 0.1.0-rc1
```

warning、failed/skipped test、vulnerability、secret scan、untracked outputを確認する。CIはUnit/IntegrationのTRXを別artifactとして保存する。
