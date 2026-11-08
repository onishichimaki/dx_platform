# ADR 0007: DxPlatformと業務アプリの責任分界、認証・認可、デプロイ履歴の登録と補完

- Status: Proposed（草案）
- Date: 2026-11-08
- Related: ADR 0002, ADR 0005, ADR 0006

## Context

DxPlatformは内部Identity（ADR 0002）でportal利用者を認証し、アプリ単位の`None/View/Use/Manage`権限で一覧・詳細・起動を制御している。起動（`/apps/{id}/launch`）は権限を再判定したうえで登録URLへredirectするだけで、アプリ側の認証・認可には関与しない。

Deployment API（`POST /api/deployments`）について:

- 全アプリ共通の単一Bearer tokenで認証する。
- これまでの呼出元はGitHub Actionsのworkflowだけだった。
- 冪等キーがないため、再送すると履歴が重複しうる。

GitHub Actionsを使わない方針（ADR 0006）に変わったため、社内サーバーの固定スクリプトから履歴を登録する必要がある。APIに到達できない場合も履歴を失わない必要がある。DxPlatformを業務アプリの認証元にすると、DxPlatform障害が全アプリ障害になり、ADR 0005に反する。

## Decision

### 1. 責任分界

| 担当 | 責任 |
|---|---|
| DxPlatform | 台帳、入口（表示・起動リンク）、デプロイ履歴の集約表示、稼働状況の可視化、台帳操作の監査、標準・テンプレート・固定スクリプトの管理 |
| 各業務アプリ | 自アプリの認証・認可、業務データ、log、Health endpoint、自アプリの固定スクリプトとjournal |
| 社内インフラ | server、IIS、AD/Entra、SQL Server、backup、正式な監視・通知、証明書、USB・ウイルスチェック手順 |

### 2. 認証・認可

- DxPlatformをアプリのIdPや認証proxyにしない。
- DxPlatformの`View/Use`は、portal上で表示・起動できるかだけを決める。アプリは直接URLでアクセスされる前提で、自分で必ず認証・認可する。これは現行仕様を明文化したもので、動作は変更しない。
- 既存アプリは現行の認証方式を維持する。
- 新規アプリは社内の認証基盤へ直接接続する（AD前提ならIIS Windows統合認証、Entraが使えるならOIDC）。DxPlatformは介在しない。方式はAD/Entraの利用可否を確認してから決める。
- DxPlatform自身のSSO化は、ADR 0002の将来課題として別ADRで判断する。本ADRでは変更しない。

### 3. 機械間認証（Deployment API）

- tokenは社内サーバー上で、実行アカウントに紐づくOS保護（DPAPI。例: `Export-Clixml`で保存したSecureString）により保存する。スクリプト本体、引数、log、journal、開発PC、USBには置かない。
- 当面は既存の単一tokenを維持する。将来はアプリ別token（hash保存、失効可能）を追加する（ADR 0005 変更D）。移行期間は既存tokenも有効とし、後方互換を保つ。
- API認証を変更する際は、`docs/AUTHORIZATION_RULES.md`、`docs/REQUIREMENTS.md`、`docs/architecture.*`を更新する。

### 4. デプロイ履歴の登録とjournal

```text
deploy-app.ps1 / rollback-app.ps1 / deploy-iis.ps1（手動実行時）
   ├ 1. journalへ記録（Status=Pending）           ← 必ず先に保存
   ├ 2. register-deployment.ps1 で POST /api/deployments
   │      201 → journalをRegistered（DxPlatformの履歴IDを保存）
   │      接続不可・5xx → Pendingのまま（デプロイ自体は成功/失敗の判定どおり）
   │      timeout等で結果不明 → Unknown
   └ 3. 後日 sync-deployment-journal.ps1 で Pending を再送
```

- journalは`<AppRoot>\<code>\journal\`にJSON Linesで保存する。DxPlatform自身の分は、その配置用に別途用意したjournalフォルダに保存する。
- 1件ごとに次を記録する。
  - クライアント生成の記録ID（GUID）
  - 対象アプリID、version、source commit、環境、実行者（OSアカウント名）
  - 開始・終了時刻（UTC）、結果（`Succeeded`/`Failed`/`RolledBack`）
  - 承認記録の識別子、パッケージのSHA-256、ローカルlogの参照
  - 登録状態と試行回数
- token、接続文字列、log全文は記録しない。
- 登録の失敗はデプロイ・rollbackの成否に影響させない。登録失敗はスクリプトの最終出力で警告する。
- `Unknown`は二重登録の恐れがある。冪等キー（ADR 0005 変更F）を導入するまでは、DxPlatformの`/deployments`画面で確認してから再送するか、`Registered`へ手動で更新する。変更Fの導入後は、記録IDを冪等キーとして送信し、無条件に再送できるようにする。
- 既存APIの項目で足りるものは追加変更なしで使う。承認記録の識別子とSHA-256は`Notes`に、ローカルlogの参照は`LogReference`に入れる。
- 照合は「journalのRegistered/Pending/Unknown件数」と「DxPlatformの履歴」を比較する固定スクリプトで行う。初期は件数と最新versionの比較とし、変更F導入後は記録ID単位で照合する。

### 5. 稼働情報の連携

```text
DxPlatform ──GET /health/ready（pull）──▶ 各アプリ   稼働状況の表示（手動、将来は定期）
サーバー固定スクリプト check-health.ps1 ──▶ 各アプリ   デプロイ直後・障害時の確認（DxPlatform非依存）
正式な監視・通知 ＝ 会社監視製品
```

- Healthの確認は、DxPlatformからのpullとサーバー側スクリプトの2系統とする。アプリ側にDxPlatform依存のagentは置かない。
- DxPlatformのHealth Checkは、既存のHost/Port/IP allowlistをそのまま使う。
- 対象アプリの識別は既存どおり`ApplicationAssetId`を使い、サーバー上のアプリ設定ファイルに記載する。

## Consequences

- DxPlatformが停止していても、アプリの利用・認証・デプロイ・rollbackは継続できる。履歴はjournalに残り、後から補完できる。
- 冪等キーを導入するまでは、`Unknown`の扱いに人の確認が必要になる。
- portalの権限とアプリ内の権限は別管理になり、二重管理が残る。将来はAD/Entraのgroupを共通の基準にして整合させる。
- 単一tokenの残存リスク（任意アプリの履歴を登録できる）は、アプリ別tokenを導入するまで、サーバー上での保存方法と実行者の限定で抑える。

## Evidence

- `src/DxPlatform.Web/Controllers/AppsController.cs`（Launchはredirectのみ）
- `src/DxPlatform.Web/Controllers/DeploymentApiController.cs`（単一token、冪等キーなし、`Notes`/`LogReference`の秘密値マーカー検査）
- `src/DxPlatform.Domain/Operations/OperationalEntities.cs`（`Deployment`の項目）
- `src/DxPlatform.Application/Security/PermissionEvaluator.cs`
- `docs/AUTHORIZATION_RULES.md`
- `docs/DECISIONS/0002-internal-authentication.md`
