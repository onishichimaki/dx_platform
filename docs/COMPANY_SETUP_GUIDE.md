# 会社環境構築手順（リリース候補版）

この手順は、自宅で検証済みの配布ZIPを会社へ持ち込み、Windows Server / IIS / SQL Server上に安全に構築するための作業順序である。会社のセキュリティ規程、変更管理、DBA・基盤担当の指示が本書より優先される。

## 0. 配布物と責任分界

使用する主成果物は `dx-platform-company-<version>.zip` である。アプリだけを配置する場合は `dx-platform-iis-<version>.zip`、DBAレビューには `dxplatform-migrations-<version>.sql` を使う。

自宅側で完了済みの範囲:

- Release build、Unit/Integration Test、依存関係・既知脆弱性確認
- SQL Server用冪等Migration SQL生成
- 秘密値を含まないProduction設定template
- IIS deploy / rollback / smoke test script
- SHA-256 checksum、source commit、dependency inventory

会社でのみ確定できる範囲:

- Windows Server、SQL Server、DNS、証明書、service account、Firewall、proxy
- GitHub Organization、Runner、Environment reviewer、Secrets/Variables
- データ分類、利用者、AppOwner、運用・監視・backup・retention
- セキュリティ、OSS、変更管理、Production移行の正式承認

## 1. 持込み・完全性確認

1. GitHub Releaseから会社端末へ配布物をdownloadする。メール添付や個人クラウドを中継しない。
2. 会社標準のmalware scanを実行する。
3. Release画面の `SHA256SUMS.txt` とdownloadファイルのhashを照合する。

```powershell
Get-FileHash .\dx-platform-company-0.1.0-rc1.zip -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

4. ZIPを専用の一時folderへ展開し、package内部も検証する。

```powershell
Expand-Archive .\dx-platform-company-0.1.0-rc1.zip .\dx-platform-company-0.1.0-rc1
.\dx-platform-company-0.1.0-rc1\scripts\test-company-package.ps1 `
  -PackageRoot .\dx-platform-company-0.1.0-rc1
```

5. `manifest.json` の `sourceCommit` が承認対象commitと一致することを確認する。
6. 展開先は管理者だけが書込み可能にし、検証後の一時fileは会社手順で削除する。

## 2. 会社Repositoryと変更管理

1. 個人Repositoryを直接Production sourceにせず、会社管理のPrivate/Internal Repositoryへ承認済みcommitを取り込む。
2. `main` と `develop` を作り、直接push禁止、review必須、CI必須、管理者迂回禁止を設定する。
3. `staging` / `production` Environmentを作り、Productionはrequired reviewerと許可branchを設定する。
4. secret scanning、Dependabot security update、code scanning、artifact retentionを会社方針に合わせる。
5. 会社固有値や秘密値を元のPublic Repositoryへpushしない。

## 3. Server事前確認

基盤担当が次を記録し、互換性チェックリストへ結果を残す。

- support中のWindows Server、IIS、PowerShell、SQL Server
- .NET 10 Hosting Bundleの会社承認済み最新patchとASP.NET Core Module V2
- HTTPS用DNS・server certificate・chain・private key ACL
- IISからSQL Serverと許可済みHealth endpointへの名前解決・port・Firewall
- deploy、backup、log、Data Protection key用folderと容量・ACL
- NTP、timezone、監視、backup、EDR、patch、service account policy

Hosting Bundle導入後は会社の変更手順に従ってIISを再起動し、`Get-WebGlobalModule -Name AspNetCoreModuleV2` でmoduleを確認する。

## 4. SQL Server構築

1. DBAがProduction/Staging database、schema owner、実行用service accountを作成する。
2. `database/dxplatform-migrations.sql` をDBAがreviewする。対象release、backup、実行者、実行日時を変更ticketへ記録する。
3. database backupを取得し、restore可能であることを確認する。
4. schema変更権限を持つ一時的なmigration実行主体で冪等SQLを適用する。passwordをcommand lineへ直書きしない。
5. アプリ実行用accountは通常運用に必要なCRUDだけへ縮小し、DDL権限を与えない。
6. `__EFMigrationsHistory` と24 table、FK/index/unique constraintを確認する。

アプリはProduction起動時にMigrationを自動適用しない。未適用Migrationがあれば起動を停止するため、`Database__ApplyMigrationsOnStartup=false` を維持する。

## 5. IIS初期構成

推奨例（実path・account・site名は会社標準に置換）:

```text
Deploy: C:\inetpub\dx-platform
Backup: D:\app-backups\dx-platform
Logs:   D:\app-logs\dx-platform
Keys:   D:\app-keys\dx-platform
AppPool managedRuntimeVersion: 空（No Managed Code）
Pipeline: Integrated
```

1. 専用application poolとsiteを作り、app pool identityを専用service accountへ設定する。
2. deploy folderは運用担当のみ変更可、app poolはRead/Executeを付与する。
3. log folderとData Protection key folderはapp poolへ必要最小限のModifyを付与する。
4. HTTPS binding、certificate、private-key Read権限を設定する。HTTPは無効化するか会社標準でHTTPSへredirectする。
5. app poolのprofileを有効にしてData Protection keyを永続化する。複数nodeでは共有key ringとcertificate保護を別途構成する。
6. recycle、idle timeout、request limit、IIS log、Failed Request Tracingを会社基準へ合わせる。

## 6. Production設定と秘密値

`config/appsettings.Production.template.json` は入力項目のtemplateであり、実値入りfileをRepositoryや配布ZIPへ戻さない。承認済みsecret store、IIS環境変数、protected configurationを使用する。

必須/重要設定:

```text
ASPNETCORE_ENVIRONMENT=Production
Database__Provider=SqlServer
Database__ApplyMigrationsOnStartup=false
ConnectionStrings__DefaultConnection=<encrypted SQL connection>
AllowedHosts=<portal.company.example>
DemoData__Enabled=false
DeploymentApi__Token=<random unique secret>
DataProtection__ApplicationName=DxPlatform-Production
DataProtection__KeyRingPath=<absolute ACL-protected key folder>
Logging__File__Path=<absolute ACL-protected log file path>
HealthChecks__AllowedHosts__0=<exact approved health host>
HealthChecks__AllowedPorts__0=443
HealthChecks__AllowPrivateAddresses=<true only for approved internal RFC1918 targets>
```

`AllowedHosts=*` とData Protection keyの相対/未設定pathはProduction起動時に拒否される。keyはWindows DPAPIでapp pool identityに保護されるため、account変更・復旧手順を事前に試験する。Health CheckはHost allowlistが空なら実行されない。Private addressを使う場合も、`AllowPrivateAddresses=true`だけでは足りず、正確なHost allowlistとFirewall制限を併用する。loopback、link-local、metadata address、redirect先は常に拒否される。

初回だけ `BootstrapAdmin__Email` / `BootstrapAdmin__Password` を一時secretとして設定する。通常SystemAdminを作成してlogin確認後、両方を削除してapp poolを再起動する。

## 7. 初回配置

1. package内 `app` folderを `dx-platform-iis-<version>.zip` と同じ形で準備する。
2. deploy scriptをまず `-WhatIf` で確認する。

```powershell
.\scripts\deploy-iis.ps1 `
  -PackagePath .\dx-platform-iis-0.1.0-rc1.zip `
  -SiteName '<site-name>' `
  -AppPoolName '<app-pool-name>' `
  -DeployPath '<deploy-path>' `
  -BackupPath '<backup-root>' `
  -BaseUrl 'https://<portal-host>' `
  -WhatIf
```

3. 変更ticket承認後に `-WhatIf` を外して実行する。scriptはbackup、site停止、file置換、開始、`/health/ready`、失敗時rollbackを行う。
4. 初回でbackupが存在しない状態の失敗時はsiteを停止したまま原因を直す。

## 8. 初期化・受入確認

次をStagingで実施し、証跡を残す。

- `/health/live` と `/health/ready` がHTTPSで200
- 初期管理者login、password変更、通常管理者作成、Bootstrap secret削除
- Viewerは許可Appだけ閲覧可能、AppOwnerは所有Appだけ編集・Health実行可能
- 直接URLで未許可App、他AppのDeployment/Health履歴へ到達できない
- login lockout、anti-forgery、429 rate limit、security headers
- App/Data/Agent CRUD、監査、Deployment API、backup/rollback
- SQL/IIS/application logにpassword、token、connection stringが出ていない
- Browser、proxy、certificate、accessibility、性能の会社基準

## 9. GitHub Actions / Runner

1. 専用Windows Self-hosted Runnerをinteractive user端末と分離し、service account、patch、EDR、networkを制限する。
2. RunnerからIIS配置先だけへ必要権限を付与し、SQL管理権限は付与しない。
3. Environment Variables/Secretsは `COMPANY_ENVIRONMENT_INPUTS.md` に従い登録する。
4. `ENABLE_STAGING_DEPLOY=false` のままdry runし、承認後にStagingだけtrueへ変更する。
5. Productionは手動dispatch + required reviewerを維持し、個人tokenではなく会社承認credentialを使用する。

## 10. 運用開始とrollback

- 正式監視は外部監視で `/health/live` / `/health/ready`、IIS、SQL、certificate、disk、Runnerを監視する。
- SQL full/differential/log backupと定期restore testを行う。
- file rollbackは `rollback-iis.ps1`、DBは自動rollbackせずDBAがforward-fixまたはrestoreを判断する。
- incident時はtoken/password/certificateをrotateし、Correlation ID、commit、GitHub run、IIS/SQL logを照合する。
- Production判定は `HANDOFF_TO_COMPANY.md` の全項目と会社承認が完了してから行う。

## Go / No-Go

次のいずれかが未完了ならProductionへ進めない: checksum不一致、未review Migration、backup/restore未確認、HTTP公開、`AllowedHosts=*`、共用管理者、Bootstrap secret残存、branch/Environment保護なし、権限UAT失敗、監視/rollback未確認、会社セキュリティまたはOSS承認なし。
