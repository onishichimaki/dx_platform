# ADR 0006: GitHub Actionsを使わないリリース・持込み・デプロイと、アプリ単位の実行方式

- Status: Proposed（草案）
- Date: 2026-11-08
- Supersedes: ADR 0004（本ADRの承認時に0004をSupersededへ変更する。それまで0004は現行記録として残す）
- Related: ADR 0005, ADR 0007

## Context

GitHubはソースコード・Issue・PRの管理に限定し、GitHub Actions、GitHub-hosted runner、Self-hosted runnerは使用しない。ビルド・テストは開発PCで行い、成果物は許可されたUSBとウイルスチェック手順で社内へ持ち込み、社内サーバーでPowerShellによりデプロイ・Health確認・rollbackを行う。各業務アプリの起動・停止・デプロイは独立させる。

既存資産として、ローカル成果物作成（`build-company-package.ps1`）、成果物検証（`test-company-package.ps1`）、IIS向けデプロイ・rollback・smoke test（`deploy-iis.ps1`ほか）がある。一方、テスト・NuGet監査・Migration差分検査はworkflowにしかない（ADR 0005 G7/G8）。Node.jsアプリの現行起動方式とサーバー環境は未確認である。

## Decision

### 1. 全体の流れ

```text
[開発PC]  clean tree + tag
   └ verify  : restore（脆弱性監査あり）→ build → test（TRX）→ 固有検査
   └ package : ZIP + manifest.json + SHA256SUMS.txt + test-summary
   └ 記録    : GitHub Issue（技術記録）と SharePoint承認記録へ version / commit / SHA-256 / テスト結果
        │
        ▼ 許可USB + ウイルスチェック（会社手順）
[社内サーバー]
   └ verify-package : 承認記録のSHA-256と照合 → 一致しなければ中止
   └ deploy        : backup/release配置 → 停止 → 切替 → 起動 → health確認 → 失敗時rollback
   └ record        : journalへ保存 → DxPlatform APIへ登録（失敗時はPendingで保持、後で補完）
```

### 2. 開発PCの固定スクリプト

- スクリプトはrepoの`scripts/`に置き、変更はPRレビューを経た場合のみとする。リリース時はスクリプトを編集しない。
- 次の条件を満たさない場合は成果物を作らない。
  - 未commit変更がない（既存の`build-company-package.ps1`の検査を継続）
  - HEADがリリースtagと一致する
  - NuGet監査を`NuGetAudit=true`、`NuGetAuditMode=all`で実行し、警告がない（Node.jsは`npm audit`、基準は別途定める）
  - Release構成のbuildとUnit/Integration testがすべて成功する
  - DxPlatformは`dotnet ef migrations has-pending-model-changes`で差分がない
- `manifest.json`へ次を記録する（既存項目に追加）。
  - version、source commit、tag、生成時刻（UTC）
  - SDK/Node版
  - 監査結果
  - テスト件数（総数・成功・失敗・スキップ、プロジェクト別）とTRXのSHA-256
  - Migration一覧
  - 秘密値を含まないことの宣言
- TRXファイルと`test-summary.json`を成果物に同梱し、`SHA256SUMS.txt`の対象に含める。
- DxPlatformは既存`build-company-package.ps1`を拡張する（新規スクリプトを乱立させない）。Node.jsアプリは同じ契約のスクリプトをテンプレートとして持つ。

### 3. 記録と持込み

- 技術的なリリース記録は、GitHubのtagとリリースIssueに置く（version、commit、SHA-256、テスト結果要約）。社内固有情報は書かない。
- 社内での承認記録はSharePointに置き、承認対象のversionとZIPのSHA-256を記載する。サーバー側では、USB内の`SHA256SUMS.txt`ではなく、この承認記録の値と照合する（USB内の値は破損検知用、承認記録の値は改ざん検知用）。
- 持込みは会社の許可USB・ウイルスチェック手順に従う。成果物には秘密値、`.env`、証明書、DBファイルを含めない（既存の禁止ファイル検査を継続）。
- Node.jsランタイム等の外部配布物も同じ手順で持ち込み、配布元が公開するhashと照合する。
- GitHub Releasesへのアップロードは必須にしない。

### 4. 社内サーバーの固定スクリプト（アプリ単位）

アプリごとに同じインターフェースのスクリプトを用意し、他アプリの操作を含めない。

| スクリプト | 役割 |
|---|---|
| `verify-package.ps1` | `-ExpectedSha256`と一致確認、`test-company-package.ps1`相当の内部checksum検証 |
| `deploy-app.ps1` | `-PackagePath -ExpectedSha256 -ApprovalReference`必須。release配置、停止、切替、起動、health確認、失敗時自動rollback、journal記録、履歴登録 |
| `rollback-app.ps1` | 直前（または指定）releaseへ戻して起動、health確認、journal記録、履歴登録（`RolledBack`） |
| `start-app.ps1` / `stop-app.ps1` / `status-app.ps1` | 起動単位の操作と状態表示 |
| `check-health.ps1` | `/health/live`・`/health/ready`の確認（既存`smoke-test.ps1`を一般化） |

- アプリ固有値（起動単位名、port、health path、配置root）はアプリrepoの`deploy/app.json`（実値なし）と、サーバー上のACL保護された設定ファイルから読む。
- 実行者は承認済みの運用アカウントとし、対象アプリの起動単位と配置フォルダだけを操作できる権限にする。GitHub Environment承認の代わりに、`-ApprovalReference`（SharePoint承認記録の識別子）を必須とし、履歴に残す。
- DxPlatform自身は既存の`deploy-iis.ps1`/`rollback-iis.ps1`を維持し、手動実行に履歴登録を組み合わせる（ADR 0007）。

### 5. 配置とrollback

```text
<AppRoot>\<code>\
   releases\<version>\     展開済み成果物（直近N世代を保持）
   current → releases\<version>   ジャンクション
   config\                 環境別設定・秘密値（ACL保護、releases外）
   logs\
   journal\                デプロイ履歴のローカル記録（ADR 0007）
```

- デプロイは「新releaseを展開 → 停止 → `current`切替 → 起動 → health確認」とし、rollbackは`current`を直前releaseへ戻すだけにする。
- DB変更はアプリデプロイと分け、前方互換のMigrationのみ・承認制とする（DxPlatformと同じ方針）。

### 6. Node.jsテンプレートの契約（framework は強制しない）

| 契約 | 内容 |
|---|---|
| コマンド | `npm ci` / `npm run lint` / `npm test` / `npm run build` / `npm start` |
| Health | `GET /health/live`、`GET /health/ready` |
| 設定 | 環境変数（またはサーバー上の設定ファイル）のみ。`.env.example`にkeyだけを記載 |
| 待受 | 原則`127.0.0.1:<PORT>`。外部公開方式は7.で決定 |
| log | 標準出力へJSON Lines、Correlation ID付与 |
| 停止 | SIGINT/SIGTERMで新規受付停止→処理完了後に終了 |
| 版固定 | `.node-version`、`package-lock.json` |
| 成果物 | 開発PCで`npm ci --omit=dev`まで実施しZIP化。本番サーバーで`npm install`しない。native moduleはサーバーと同じOS/architectureでbuildする |

```text
<app-repo>/
├─ AGENTS.md / CLAUDE.md      AI向け指示（ADR 0008）
├─ README.md, docs/RUNBOOK.md, docs/ARCHITECTURE.md, docs/DECISIONS/, docs/HANDOFF.md
├─ .node-version, package.json, package-lock.json, .env.example
├─ src/  test/
├─ deploy/app.json            起動単位名、port、health path（実値なし）
└─ scripts/                   verify / package（開発PC）、deploy / rollback / start / stop / status / check-health（サーバー）
```

既存アプリは契約部分だけを後付けすればよい。

### 7. 実行方式（Windows service化・IIS reverse proxy）は環境確認後に決める

フェーズ0の調査結果で、アプリごとに次の候補から選ぶ。決定時は本ADRを更新するか、個別ADRを追加する。

| 候補 | 概要 | 確認事項 |
|---|---|---|
| A 現行方式維持 | 既存の起動方式を固定スクリプトで包む | 独立停止・自動起動・異常時再起動ができるか |
| B Windows service | WinSW等でアプリごとにservice化 | OSS導入審査、service作成の管理権限 |
| C IIS配下 | HttpPlatformHandler/ANCMでIISがプロセス起動 | moduleの導入状況とサポート状況 |
| D 外部公開 | IIS reverse proxy（URL Rewrite + ARR）またはNodeの直接待受 | IIS機能の導入可否、証明書運用、port開放方針 |

判断基準（必須）:

- アプリ単位で起動・停止・デプロイ・rollbackでき、他アプリに影響しない。
- OS再起動後に自動起動し、異常終了時に再起動できる。
- logと設定がアプリ単位で分離される。
- 会社の権限・OSS・セキュリティ基準を満たす。

採用しない方式は次のとおり。

- **iisnode:** メンテナンスが停止している。
- **pm2:** 全アプリを単一のプロセスマネージャで管理する構成で、共通の障害点になる。

### 8. 既存GitHub Actions workflowの停止

現状（2026-11-08確認）:

| workflow | 自動実行条件 | 実際の影響 |
|---|---|---|
| `ci.yml` | `main`/`develop`/`feature/**`/`fix/**`/`docs/**`/`agent/**`へのpush、全PR | GitHub-hosted runnerで実行。現在はNuGet監査で失敗中（ADR 0005 G8） |
| `release.yml` | `v*` tagのpush（手動起動も可） | 検証後にGitHub prereleaseを作成（`contents: write`） |
| `deploy-staging.yml` | `main`へのpush（手動起動も可） | `ENABLE_STAGING_DEPLOY`未設定のためjobはskip。`main`ブランチも存在しない |
| `deploy-production.yml` | 手動起動のみ | 自動実行なし |

あわせて次も確認した。

- branch protectionとrulesetは未設定。必須status checkもない。
- Variables・Secrets・Environmentsは0件、登録runnerも0件。
- リポジトリ設定のActionsは「有効・全action許可」。

停止案（安全な順）:

| 手順 | 内容 | 性質 |
|---|---|---|
| 1 | リポジトリ設定でActionsを無効化する（Settings → Actions → General → Disable actions。API: `PUT /repos/{owner}/{repo}/actions/permissions`で`enabled=false`） | 即時・可逆・ファイル変更なし。後から追加されたworkflowも実行されない。リポジトリ管理者が行う |
| 2 | 別PRで、ローカル検証・成果物作成・履歴登録スクリプトを整備し、`.github/workflows/*.yml`を削除する | 恒久化。後任者やAIがworkflowを前提にしないようにする。履歴はgitに残る |
| 3 | 同じPRで関連文書を更新し、本ADRの承認とADR 0004のSuperseded化を行う | 文書整合 |

個別の`gh workflow disable`は、新規追加workflowを止められないため補助手段とする。

影響範囲:

- push/PRでの自動検証がなくなる。必須checkは設定されていないため、マージは妨げられない。代わりにローカル検証結果（`test-summary.json`の要約）をPR本文に記載する。
- `release.yml`の停止により、tag push時のprerelease自動作成がなくなる。既存のRelease `v0.1.0-rc1`とその添付ファイルは、Actions無効化の影響を受けない。
- 過去のrun logとartifactは保持期間内なら閲覧でき、無効化で即時削除されない。
- `deploy-*.yml`はrunner・Variables・Secrets・Environmentがないため、実運用への影響はない。
- 文書の更新対象（手順2・3のPR）: `README.md`、`docs/DEPLOYMENT.md`、`docs/OPERATIONS.md`、`docs/COMPANY_SETUP_GUIDE.md`、`docs/HANDOFF_TO_COMPANY.md`、`docs/COMPANY_ENVIRONMENT_INPUTS.md`、`docs/COMPANY_COMPATIBILITY_CHECKLIST.md`、`docs/ARCHITECTURE.md`、`docs/architecture.*`、`docs/SECURITY.md`、`docs/TEST_PLAN.md`、`docs/ROADMAP.md`、`docs/ASSUMPTIONS.md`、`docs/KNOWN_LIMITATIONS.md`、`docs/IMPLEMENTATION_PLAN.md`、`docs/DECISIONS/0004-github-actions-and-iis.md`。
- 手順1の前に失われる検証（テスト、NuGet監査、Migration差分検査）は、手順2が完了するまで`AGENTS.md`記載のコマンドとNuGet監査付きrestoreを開発PCで手動実行して補う。

## Consequences

- GitHubとrunnerに依存せず、社内持込み手順と整合したリリースになる。runner・Environment・Secretsの構築と管理が不要になる。
- 検証の実施は開発PCの固定スクリプトと人の確認に依存する。スクリプトのPRレビュー、clean tree・tag一致の強制、`manifest.json`へのテスト結果記録で補う。
- 承認の証跡は、GitHub Environmentに代えてSharePoint承認記録と`-ApprovalReference`で残す。
- 実行方式は環境確認まで決まらない。ただし固定スクリプトのインターフェースを先に揃えるため、方式が決まった後の差し替え範囲はスクリプト内部に限られる。
- サーバー操作は人が行うため、手順と固定スクリプトの品質がそのまま復旧時間に効く。rollback演習を引継ぎ条件とする（ADR 0008）。

## Evidence

- `scripts/build-company-package.ps1`、`scripts/test-company-package.ps1`
- `scripts/deploy-iis.ps1`、`scripts/rollback-iis.ps1`、`scripts/smoke-test.ps1`
- `.github/workflows/ci.yml`、`.github/workflows/release.yml`、`.github/workflows/deploy-staging.yml`、`.github/workflows/deploy-production.yml`
- `docs/COMPANY_SETUP_GUIDE.md`（手動デプロイ手順）
- `docs/DECISIONS/0004-github-actions-and-iis.md`
