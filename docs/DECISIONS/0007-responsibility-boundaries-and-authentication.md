# ADR 0007: DxPlatformと業務アプリの責任分界、認証・認可、台帳連携

- Status: Proposed（草案）
- Date: 2026-11-08
- Related: ADR 0002, ADR 0005

## Context

DxPlatformは内部Identity（ADR 0002）でportal利用者を認証し、アプリ単位の`None/View/Use/Manage`権限で一覧・詳細・起動を制御している。起動（`/apps/{id}/launch`）は権限再判定後に登録URLへredirectするだけで、アプリ側の認証・認可には関与しない。Deployment APIは全アプリ共通の単一Bearer tokenで認証する。

DxPlatformを業務アプリの認証元にすると、DxPlatform障害が全アプリ障害になり、ADR 0005の「実行時依存を持たせない」に反する。

## Decision

### 1. 責任分界

| 担当 | 責任 |
|---|---|
| DxPlatform | 台帳、入口（表示・起動リンク）、デプロイ履歴、稼働状況の可視化、台帳操作の監査、標準・テンプレート（共通repo）の管理 |
| 各業務アプリ | 自アプリの認証・認可、業務データ、log、Health endpoint、共通workflowを使った自アプリのデプロイ |
| 社内インフラ | server、IIS、AD/Entra、SQL Server、backup、正式な監視・通知、証明書 |

### 2. 認証・認可

- DxPlatformをアプリのIdPや認証proxyにしない。
- DxPlatformの`View/Use`はportal上の表示・起動の可否のみを決める。アプリは直接URLアクセスを前提に、自分で必ず認証・認可する。これは現行仕様の明文化であり、動作は変更しない。
- 既存アプリは現行の認証方式を維持する。
- 新規アプリは社内認証基盤へ直接接続する（AD前提ならIIS Windows統合認証、Entra利用可ならOIDC）。DxPlatformは介在しない。採用方式はAD/Entraの利用可否確認後に決める。
- DxPlatform自身のSSO化はADR 0002の将来課題として別ADRで判断し、本ADRでは変更しない。

### 3. 機械間認証（Deployment API）

- 当面は既存の単一tokenを維持し、GitHub `production`/`staging` Environment secretに限定して配布する。
- 将来、アプリ別token（hash保存、失効可能）を追加する。移行期間は既存tokenも有効とし後方互換を保つ。API認証の変更のため、実装時に`docs/AUTHORIZATION_RULES.md`、`docs/REQUIREMENTS.md`、`docs/architecture.*`を更新する。

### 4. 台帳とデプロイ・稼働情報の連携

```text
各アプリのGitHub Actions ──deploy──▶ Windows Server（service + IIS）
        │ 終了後に登録（continue-on-error、最大3回retry）
        ▼
DxPlatform  POST /api/deployments ──▶ 履歴、（変更A）台帳Version/LastDeployedAt
DxPlatform ──GET /health/ready（pull）──▶ 稼働状況表示（手動、将来は定期）
正式監視・通知 ＝ 会社監視製品
```

- アプリはDxPlatformへpushするのはデプロイ履歴のみで、DxPlatform停止中も成功扱いで継続する。未登録分はGitHub Actions記録から補完する。
- Health確認はDxPlatformからのpullとし、アプリ側にDxPlatform依存のagentを置かない。既存のHost/Port/IP allowlistをそのまま使う。
- 対象アプリの識別は既存どおり`ApplicationAssetId`（GitHub Variable）を使う。

## Consequences

- DxPlatform障害時もアプリの利用・認証・デプロイが継続する。
- portal権限とアプリ内権限は別管理になり、二重管理が残る。AD/Entraのgroupを共通の基準にすることで将来的に整合させる。
- 単一tokenの残存リスク（任意アプリの履歴登録）は、アプリ別token導入まで運用で抑える。

## Evidence

- `src/DxPlatform.Web/Controllers/AppsController.cs`（Launchはredirectのみ）
- `src/DxPlatform.Web/Controllers/DeploymentApiController.cs`（単一token）
- `src/DxPlatform.Application/Security/PermissionEvaluator.cs`
- `docs/AUTHORIZATION_RULES.md`
- `docs/DECISIONS/0002-internal-authentication.md`
