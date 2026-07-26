# 会社引継ぎチェックリスト

Repositoryには会社固有値を記録しない。実値、承認記録、担当者名は会社管理のticket/secret/configuration systemに置く。

## 1. Repository・governance

- [ ] 個人GitHubから会社管理Repositoryへ履歴を保って移管する
- [ ] source code/文書/架空dataに会社外持込禁止情報がないことを再確認する
- [ ] .NET、NuGet、Bootstrap、jQuery等のlicense/OSS packageを確認する
- [ ] GitHub Organization、team、least privilegeを設定する
- [ ] `main`/`develop`のbranch protection、required review/check、signed commit方針を設定する
- [ ] GitHub `staging`/`production` EnvironmentとProduction required reviewerを設定する
- [ ] secret scanning、Dependabot、code scanningの利用可否を確認する

## 2. Platform互換性

- [ ] Windows Server version/support期限を確認する
- [ ] IIS role service、WebAdministration module、ASP.NET Core moduleを確認する
- [ ] 会社承認済み.NET 10 SDK/runtime/Hosting Bundle patchを確認する
- [ ] SQL Server version/edition/collation/HA/backup policyを確認する
- [ ] Self-hosted Runner versionがNode.js 24 action要件を満たすことを確認する
- [ ] browser、TLS、proxy/firewall、DNS、certificate chainを確認する

## 3. SQL Server

- [ ] Production/Staging DBを作成する
- [ ] application service account/DB userを作成し最小権限を付与する
- [ ] encrypted connection stringをsecret store/IIS環境変数へ設定する
- [ ] 全Migrationsの冪等SQLをDBA reviewする
- [ ] backup取得後にMigrationを実行する
- [ ] table/index/unique/check/FK、timezone、collationを確認する
- [ ] full/differential/log backupとrestore testを設定する
- [ ] AuditLog retention/容量/archiving方針を決める

## 4. IIS

- [ ] IIS siteを作成する
- [ ] No Managed Codeのapplication poolを作成する
- [ ] service accountとlogon policyを確認する
- [ ] deploy/backup/log folderを作りACLを設定する
- [ ] Production URL/DNSを設定する
- [ ] SSL certificate/binding/private-key ACL/更新手順を設定する
- [ ] Hosting Bundle install後にIISを再起動/確認する
- [ ] Production環境変数を設定し、SQLiteが指定されていないことを確認する
- [ ] `/health/live`と`/health/ready`をnetwork経路ごと確認する
- [ ] request/log/recycle/idle timeout設定を会社基準へ合わせる

## 5. GitHub Actions/CD

- [ ] Windows Self-hosted Runnerを専用account/hostへ設定する
- [ ] Runner group/label/network/working folder/patch policyを設定する
- [ ] IIS/URL/application asset ID等のVariablesをEnvironmentへ設定する
- [ ] Deployment API token等のSecretsをEnvironmentへ設定する
- [ ] `ENABLE_STAGING_DEPLOY`を初期はfalse、承認後trueにする
- [ ] Staging backup/deploy/smoke/rollback/履歴登録を検証する
- [ ] Production manual dispatchとapprovalをdry runする
- [ ] GitHub Actions log/artifact retentionを設定する
- [ ] mail/Teams等の成功・失敗通知を会社標準で設定する

## 6. Application初期化

- [ ] uniqueなBootstrapAdmin email/passwordをsecret経由で一時設定する
- [ ] 初回起動とrole/admin作成を確認する
- [ ] 通常SystemAdminを登録し、Bootstrap secretを削除/rotateする
- [ ] Development demo dataをProductionへ投入していないことを確認する
- [ ] 実user、group、role、App permissionを承認記録に基づき登録する
- [ ] 実App/Data/Agent/Toolをowner確認の上で登録する
- [ ] 本物のprompt/credential/connection stringがcatalogにないことを確認する

## 7. Operations・release

- [ ] application/file/IIS/SQL/GitHub logの保管・閲覧権限を設定する
- [ ] infrastructure/app/DB監視とon-call/escalationを設定する
- [ ] backup retentionとrestore/rollback訓練を行う
- [ ] vulnerability/patch/license review cadenceを決める
- [ ] UATを実施し、role/permission/直接URL/disabled userを重点確認する
- [ ] security review、privacy review、operation reviewを完了する
- [ ] Production移行承認を得る
- [ ] 運用担当へrunbook、known limitations、contact、権限棚卸しを引き継ぐ
