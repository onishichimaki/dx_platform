# ADR 0005: DxPlatformを業務アプリの「台帳・入口・履歴」に限定し、実行時依存とCI依存を持たせない

- Status: Proposed（草案。承認前のためコード変更は行わない）
- Date: 2026-11-08
- Related: ADR 0004（承認時にADR 0006で置換）, ADR 0006, ADR 0007, ADR 0008

## Context

DxPlatformを社内Node.js業務アプリの共通開発・運用基盤として発展させたい。完成目標は、担当者が異動しても後任者がAIと固定スクリプトを使い、安全に改修・リリース・復旧できることである。

制約:

- 既存のASP.NET Core MVC（.NET 10）による資産台帳・ポータル、DB、認証は維持する。
- Factory Lensとスキルマップは独立運用とし、標準化対象外とする（台帳登録のみ）。
- 共通化によって障害影響範囲を拡大しない。共通基盤自体の障害時も既存アプリの利用を継続できること。
- GitHubはソースコード・Issue・PRの管理に限定し、GitHub Actions、GitHub-hosted runner、Self-hosted runnerは使用しない。
- ビルド・テストは開発PCで行い、成果物は許可されたUSBとウイルスチェック手順で社内へ持ち込む。
- 過剰設計を避け、既存資産を最大限活用する。

### 現状調査（2026-11-08時点）

既に実現できているもの:

| 領域 | 内容 | 根拠 |
|---|---|---|
| アプリ台帳 | Code、URL、種別、技術スタック、部署/管理者/Owner/開発リード、GitHub URL、RuntimeEnvironment、Status、Version、HealthCheckUrl、機密区分、最終確認日 | `src/DxPlatform.Domain/Applications/ApplicationAsset.cs` |
| 入口 | 権限で絞った一覧・詳細・起動、直接URL拒否 | `src/DxPlatform.Web/Controllers/AppsController.cs` |
| デプロイ履歴 | `POST /api/deployments`（Bearer token）。呼出元はHTTPクライアントであればよく、GitHub Actionsに依存しない | `DeploymentApiController.cs` |
| 稼働確認 | SSRF対策付き手動Health Check | `src/DxPlatform.Infrastructure/Security/HealthCheckTargetValidator.cs` |
| 監査 | AuditLog、Correlation ID、秘密値非記録のfile log | `AuditService`、`SafeFileLoggerProvider` |
| ローカル成果物作成 | 未commit変更の拒否、publish、冪等Migration SQL、依存一覧、`manifest.json`（version、source commit、生成時刻、Migration一覧）、禁止ファイル検査、`SHA256SUMS.txt`、ZIP作成 | `scripts/build-company-package.ps1` |
| 成果物検証 | 必須ファイル、SHA-256一致、一覧外ファイルの検出 | `scripts/test-company-package.ps1` |
| サーバー側操作 | IIS向けbackup、停止→配置→起動、`/health/ready` smoke test、失敗時rollback。手動実行手順あり | `scripts/deploy-iis.ps1`、`scripts/rollback-iis.ps1`、`scripts/smoke-test.ps1`、`docs/COMPANY_SETUP_GUIDE.md` |

不足・不整合（本ADRでは記録のみ。修正は個別に承認する）:

| # | 内容 |
|---|---|
| G1 | Deployment APIが台帳の`LastDeployedAt`/`Version`を更新しない。 |
| G2 | URL/HealthCheckUrlが1件ずつで、環境（Staging/Production）ごとのserver、起動単位名、portを持てない。 |
| G3 | SharePoint業務資料、Runbook、副担当の参照項目がない。 |
| G4 | 定期Health Checkがない（`IHealthCheckScheduler`は`ManualOnlyHealthCheckScheduler`のみ）。 |
| G5 | Deployment API tokenが全アプリ共通で、任意アプリの履歴を登録できる。 |
| G6 | `deploy-iis.ps1`は`DxPlatform.Web.dll`前提でNode.jsアプリに使えない。 |
| G7 | テスト、NuGet脆弱性監査、Migration差分検査はGitHub Actions（`ci.yml`/`release.yml`）にしかなく、`build-company-package.ps1`はこれらを実行せず、テスト結果も`manifest.json`に記録しない。 |
| G8 | 2026-11-08時点でCIのNuGet監査（`NuGetAuditMode=all`、警告をエラー扱い）が推移的依存の既知脆弱性で失敗している。ローカルの既定restoreでは検出されなかったため、ローカル手順でも同じ監査条件が必要である。依存更新は別PRで扱う。 |
| G9 | デプロイ履歴の登録処理がworkflow内にしかなく、ローカル・社内サーバーから登録するスクリプトがない。APIに到達できない場合の保存・補完手段もない。 |
| G10 | 自動rollback時も結果が`Failed`になり`RolledBack`が使われない。workflowの登録では`StartedAt`/`FinishedAt`がともに登録時刻になる。 |
| G11 | Deployment APIに冪等キーがなく、再送時に履歴が重複しうる。 |
| G12 | copy方式の配置で停止時間・rollback時間が長い。 |
| G13 | Node.jsテンプレート、アプリ単位の固定スクリプト、アプリ用AI指示テンプレートがない。 |
| G14 | 認証は内部Identityのみ（ADR 0002）。 |
| G15 | 成果物検査スクリプトに開発PC固有の識別子が直書きされている。会社持込み前に設定化または削除を検討する。 |

## Decision

1. DxPlatformの責務は「台帳」「入口（表示・起動リンク）」「デプロイ履歴」「稼働状況の可視化」「標準・テンプレートの管理」に限定する。
2. 業務アプリはDxPlatformへ実行時依存しない。DxPlatformを認証元、認証proxy、通信中継、プロセス起動制御にしない。DxPlatform停止中も各アプリは直接URLで利用でき、デプロイ・rollback・障害対応も継続できる。
3. ビルド・テスト・成果物作成は開発PCの固定PowerShellスクリプトで、デプロイ・Health確認・rollbackは社内サーバーの固定PowerShellスクリプトで行う（ADR 0006）。GitHub Actions、runner、GitHub Environment承認には依存しない。
4. 共通化はコードの共有ではなく「契約（約束事）」と「バージョン管理された固定スクリプト」で行う。
5. デプロイ履歴の一次記録は社内サーバー上のローカル履歴（journal）とし、DxPlatformへはAPIで登録する。APIが使えない場合もjournalに保存し、後で照合・補完する（ADR 0007）。
6. 正式な監視・通知は会社監視製品の責務とし、DxPlatformのHealth表示は可視化に留める。
7. DxPlatform本体への変更は追加のみとし、次を個別に承認・実装する。

| 変更 | 内容 | 規模 |
|---|---|---|
| A | Deployment APIが成功時に`Version`/`LastDeployedAt`を更新（G1、スキーマ変更なし） | 小 |
| B | SharePoint URL、Runbook URL、副担当のnullable列追加（G3） | 小（Migration、`DATA_MODEL.md`更新） |
| C | 環境別情報の子テーブル（環境名、server、起動単位名、port、URL、Health URL）（G2） | 中 |
| D | アプリ別Deployment token（G5、ADR 0007） | 中 |
| E | `IHealthCheckScheduler`の定期実行実装（G4、任意） | 中 |
| F | Deployment APIの冪等キー（クライアント生成ID、アプリ単位で一意）。重複時は既存IDを返す（G11、ADR 0007） | 小〜中（Migration） |

### 実装フェーズ

| フェーズ | 内容 | 既存アプリ・DBへの影響 |
|---|---|---|
| 0 調査・合意 | Node.jsアプリ棚卸し（repo、起動方式、port、server、認証、DB、担当）、サーバー環境確認（IIS機能、管理権限、OSS導入可否）、USB・ウイルスチェック手順と社内でのAI利用可否の確認、本ADR群の承認、GitHub Actionsのリポジトリ設定での無効化（ADR 0006） | なし |
| 1 DxPlatformのローカル化 | 別PRで、ローカル検証・成果物作成の拡張（G7/G8）、履歴登録・journal・補完スクリプト（G9）、workflowファイル削除、関連文書更新、ADR 0004の置換。新手順でStagingへ1回リリース | なし（スクリプト・文書のみ） |
| 2 パイロット | Node.jsテンプレートとアプリ単位の固定スクリプト。低影響アプリ1件でStaging→本番、rollback演習。変更A | DxPlatform: API処理追加（テスト追加） |
| 3 横展開 | 残りアプリを1件ずつ移行。変更B、C、D、F（Eは任意） | 追加列/表のMigration。token・API互換を維持 |
| 4 将来 | SSO（DxPlatform・各アプリ個別）を別ADRで判断 | 別途 |

## Consequences

- DxPlatform障害が業務アプリの利用・デプロイ・rollbackに波及しない。DxPlatformの情報は実態より遅れる場合があり、journalからの補完手順が必要になる。
- GitHub側の自動検証がなくなるため、検証の実施と結果記録はローカル固定スクリプトと人の確認に依存する。スクリプトはPRレビューを経てのみ変更する。
- 既存DB・認証は変更しない。DxPlatform自身のサーバー側デプロイ手順（`deploy-iis.ps1`）は維持し、起動方法がworkflowから手動実行に変わる。
- Factory Lensとスキルマップは台帳登録のみで、テンプレート・デプロイ・認証に関与しない。

## Open questions

- 標準化対象Node.jsアプリの一覧と現在の起動方式。
- server台数・構成、IIS機能（URL Rewrite/ARR等）の導入状況、Windows service化に使えるツールと管理権限。
- 許可されたUSB・ウイルスチェック手順、社内へ持ち込める成果物の形式と承認者。
- 社内ネットワーク内でのAI（Claude Code/Codex）利用可否と、ログ等を社外の開発PCへ持ち出せる範囲。
- AD/Entra IDの利用可否。

## Evidence

- `src/DxPlatform.Domain/Applications/ApplicationAsset.cs`
- `src/DxPlatform.Domain/Operations/OperationalEntities.cs`
- `src/DxPlatform.Web/Controllers/DeploymentApiController.cs`
- `src/DxPlatform.Application/Abstractions/OperationalServices.cs`
- `scripts/build-company-package.ps1`、`scripts/test-company-package.ps1`
- `scripts/deploy-iis.ps1`、`scripts/rollback-iis.ps1`、`scripts/smoke-test.ps1`
- `.github/workflows/ci.yml`、`.github/workflows/release.yml`
- `docs/COMPANY_SETUP_GUIDE.md`、`docs/KNOWN_LIMITATIONS.md`
