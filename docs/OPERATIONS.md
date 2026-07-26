# 運用ガイド

## 日常確認

1. `/health/live`と`/health/ready`、IIS site/app pool、SQL Server接続を確認する。
2. Dashboardの停止中、責任者/機密区分未設定、最終確認90日超過を確認する。
3. 失敗Deployment、Health Check失敗、login lockout、監査失敗を確認する。
4. backup、log転送、disk容量、certificate期限、Runner状態を会社監視で確認する。

## user/権限運用

- user退職・異動時は物理削除せず無効化し、security stampでsessionを失効する。
- roleとgroupは最小権限で申請・承認記録を残し、定期棚卸しする。
- user個別Noneは例外的な明示拒否として扱い、理由をRepository外の承認記録に残す。
- BootstrapAdminは初回だけ利用し、通常管理者を作成後に環境変数を削除/rotateする。

## asset運用

- owner、manager、機密区分、最終確認日、statusを定期更新する。
- Dataの接続先参照名はsecret store等の論理名だけを登録する。
- AgentにSystem Prompt本文、API key、credential、個人情報を登録しない。
- 無効化前に確認画面と影響資産を確認する。復活が必要な場合は管理者が`IsActive`を安全な承認手順で戻す機能を将来追加する。

## manual Health Check

管理menuの稼働確認から実行する。同一Appは30秒に1回まで、timeoutは1–60秒の設定範囲である。response bodyを保存しない。失敗時は対象側のHealth endpoint、network/firewall、certificate、DNSを確認する。定期監視は未実装なので会社監視製品を正式監視にする。

## log/監査

- Console/file logの出力先、ACL、保存期間、rotation、collectorを会社基準で設定する。
- AuditLogはDB backup対象。業務dataと同じretentionにせず、会社の監査要件で決定する。
- 障害調査は利用者画面のCorrelation ID、request時刻、user ID、asset/deployment IDで絞る。
- logへsecretが疑われる場合はアクセスを制限し、rotate/削除方針をsecurity担当と決める。

## backup/restore

- SQL Serverは会社標準のfull/differential/log backupとrestore testを構成する。
- IIS deploy scriptは各配置直前のfile backupをtimestamp folderへ作成する。
- file backupのretentionはdisk capacityとrelease policyに合わせて運用jobで管理する。本MVP scriptは過去backupを自動削除しない。
- rollback後もDB schema rollbackは自動化しない。破壊的Migrationを避け、DB forward-fix/restore判断をDBAと行う。

## incident/rollback

1. 新release障害ならtraffic/利用を止め、GitHub runとDeployment履歴を確認。
2. deploy scriptが自動rollbackしたかIIS/application logで確認。
3. 必要なら承認済みbackup pathを指定して`rollback-iis.ps1`を実行。
4. `/health/ready`、login、主要catalogを確認。
5. 原因、影響、commit、run、rollback、再発防止を監査可能な場所へ記録。

