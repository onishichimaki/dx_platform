# ADR 0005: DxPlatformを業務アプリの「台帳・入口・履歴」に限定し、実行時依存を持たせない

- Status: Proposed（草案。承認前のためコード変更は行わない）
- Date: 2026-11-08
- Related: ADR 0006, ADR 0007, ADR 0008

## Context

DxPlatformを社内Node.js業務アプリの共通開発・運用基盤として発展させたい。最優先の目的は、開発者が異動しても後任者がClaude Code/Codexを使って安全に開発・改修・デプロイ・障害対応できることである。

制約:

- 既存のASP.NET Core MVC（.NET 10）による資産台帳・ポータル、DB、認証、デプロイ方式は維持する。
- Factory Lensとスキルマップは独立運用とし、標準化対象外とする（台帳登録のみ）。
- 共通化によって障害影響範囲を拡大しない。共通基盤自体の障害時も既存アプリの利用を継続できること。
- 過剰設計を避け、既存資産を最大限活用する。

### 現状調査（2026-11-08時点）

既に実現できているもの:

| 領域 | 内容 | 根拠 |
|---|---|---|
| アプリ台帳 | Code、URL、種別、技術スタック、部署/管理者/Owner/開発リード、GitHub URL、RuntimeEnvironment、Status、Version、HealthCheckUrl、機密区分、最終確認日 | `src/DxPlatform.Domain/Applications/ApplicationAsset.cs` |
| 入口 | 権限で絞った一覧・詳細・起動、直接URL拒否 | `src/DxPlatform.Web/Controllers/AppsController.cs` |
| デプロイ履歴 | `POST /api/deployments`（Bearer token）とGitHub Actionsからの登録例 | `DeploymentApiController.cs`、`.github/workflows/deploy-production.yml` |
| 稼働確認 | SSRF対策付き手動Health Check | `src/DxPlatform.Infrastructure/Security/HealthCheckTargetValidator.cs` |
| 監査 | AuditLog、Correlation ID、秘密値非記録のfile log | `AuditService`、`SafeFileLoggerProvider` |
| デプロイ部品 | IIS向けbackup、停止→配置→起動、smoke test、失敗時rollback | `scripts/deploy-iis.ps1`、`scripts/rollback-iis.ps1` |

不足・不整合（本ADRでは記録のみ。修正は個別に承認する）:

| # | 内容 |
|---|---|
| G1 | Deployment APIが台帳の`LastDeployedAt`/`Version`を更新しない。 |
| G2 | URL/HealthCheckUrlが1件ずつで、環境（Staging/Production）ごとのserver、service名、portを持てない。 |
| G3 | SharePoint業務資料、Runbook、副担当の参照項目がない。 |
| G4 | 定期Health Checkがない（`IHealthCheckScheduler`は`ManualOnlyHealthCheckScheduler`のみ）。 |
| G5 | Deployment API tokenが全アプリ共通で、任意アプリの履歴を登録できる。 |
| G6 | `deploy-iis.ps1`は`DxPlatform.Web.dll`前提でNode.jsアプリに使えない。 |
| G7 | 本番workflowがdeploy時にsourceから再buildしており、ADR 0004の「同一artifact昇格」と一致しない。 |
| G8 | 自動rollback時も結果が`Failed`で`RolledBack`が使われない。`StartedAt`/`FinishedAt`が登録時刻になる。 |
| G9 | copy方式の配置で停止時間・rollback時間が長い。 |
| G10 | Node.jsテンプレート、共通deploy workflow、アプリ用AI指示テンプレートがない。 |
| G11 | 認証は内部Identityのみ（ADR 0002）。 |

## Decision

1. DxPlatformの責務は「台帳」「入口（表示・起動リンク）」「デプロイ履歴」「稼働状況の可視化」「標準・テンプレートの管理」に限定する。
2. 業務アプリはDxPlatformへ実行時依存しない。DxPlatformを認証元、認証proxy、通信中継、プロセス起動制御にしない。DxPlatform停止中も各アプリは直接URLで利用でき、デプロイ・障害対応も継続できる。
3. 共通化はコードの共有ではなく「契約（約束事）」と「バージョン固定した部品」で行う（ADR 0006）。
4. デプロイ履歴の正本はGitHub Actionsの実行記録とし、DxPlatformは索引とする。履歴登録の失敗はアプリのデプロイ結果を失敗にしない。
5. 正式な監視・通知は会社監視製品の責務とし、DxPlatformのHealth表示は可視化に留める。
6. DxPlatform本体への変更は追加のみとし、次の順で個別に承認・実装する。

| 変更 | 内容 | 規模 |
|---|---|---|
| A | Deployment APIが成功時に`Version`/`LastDeployedAt`を更新（G1、スキーマ変更なし） | 小 |
| B | SharePoint URL、Runbook URL、副担当のnullable列追加（G3） | 小（Migration、`DATA_MODEL.md`更新） |
| C | 環境別情報の子テーブル（環境名、server、service名、port、URL、Health URL）（G2） | 中 |
| D | アプリ別Deployment token（G5、ADR 0007） | 中 |
| E | `IHealthCheckScheduler`の定期実行実装（G4、任意） | 中 |

### 実装フェーズ

| フェーズ | 内容 | 既存影響 |
|---|---|---|
| 0 調査・合意 | Node.jsアプリ棚卸し（repo、起動方式、port、server、認証、DB、担当）、AD/Entra可否、GitHub plan、本ADR群の承認 | なし |
| 1 標準整備 | Node.jsテンプレートrepo、共通deploy repo、AI指示/Runbookテンプレート。低影響アプリ1件でStaging検証 | なし（新規repoのみ） |
| 2 パイロット | 1アプリを本番移行（並行稼働→IISルール切替）、rollback演習、変更A | DxPlatform: API処理追加（テスト追加） |
| 3 横展開 | 残りアプリを1件ずつ移行、変更B〜D（Eは任意） | 追加列Migration、token後方互換 |
| 4 将来 | SSO（DxPlatform・各アプリ個別）を別ADRで判断 | 別途 |

## Consequences

- DxPlatform障害が業務アプリ障害に波及しない。代わりにDxPlatformの情報はGitHub/各サーバーの実態より遅れる場合があり、補完手順が必要になる。
- 既存DB・認証・デプロイ方式は変更しない。G7/G8はDxPlatform自身のデプロイの課題として別途扱う。
- Factory Lensとスキルマップは台帳登録のみで、テンプレート・デプロイ・認証に関与しない。
- 標準化の成果は新規repoと文書が中心で、DxPlatform本体の変更量は小さい。

## Open questions

- 標準化対象Node.jsアプリの一覧と現在の起動方式（pm2/iisnode/手動/タスクスケジューラ等）。
- server台数・構成、serverからのインターネット到達性。
- AD/Entra IDの利用可否。
- GitHub契約plan、Environment承認、Self-hosted Runner設置の可否。

## Evidence

- `src/DxPlatform.Domain/Applications/ApplicationAsset.cs`
- `src/DxPlatform.Domain/Operations/OperationalEntities.cs`
- `src/DxPlatform.Web/Controllers/DeploymentApiController.cs`
- `src/DxPlatform.Application/Abstractions/OperationalServices.cs`
- `scripts/deploy-iis.ps1`
- `.github/workflows/deploy-production.yml`
- `docs/KNOWN_LIMITATIONS.md`
