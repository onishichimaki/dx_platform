# ADR 0006: Node.js業務アプリの共通テンプレートとWindows Serverでのアプリ単位運用

- Status: Proposed（草案）
- Date: 2026-11-08
- Related: ADR 0004, ADR 0005

## Context

標準化対象のNode.js業務アプリを、Windows Server上でアプリ単位に独立して起動・停止・デプロイ・ロールバックできるようにしたい。共通化によって障害影響範囲が広がってはならない。既存の`scripts/deploy-iis.ps1`はDxPlatform専用（`DxPlatform.Web.dll`前提）のcopy方式であり、Node.jsにはそのまま使えない。既存アプリの現行起動方式は未確認である（ADR 0005 Open questions）。

## Decision

### 1. テンプレートは「契約」を標準化し、framework は強制しない

```text
<app-repo>/
├─ AGENTS.md / CLAUDE.md      AI向け指示（ADR 0008）
├─ README.md                  概要、ローカル起動、関連資料の所在（社内URLは書かない）
├─ docs/RUNBOOK.md            起動停止、log場所、rollback、典型障害
├─ docs/ARCHITECTURE.md, docs/DECISIONS/, docs/HANDOFF.md
├─ .node-version              Node版固定（package.json enginesと一致）
├─ package.json / package-lock.json
├─ .env.example               設定keyのみ（値なし）
├─ src/  test/
├─ deploy/app.json            service名、port、health path等（環境別実値は書かない）
└─ .github/workflows/ci.yml, deploy.yml   共通workflowを呼ぶだけ
```

| 契約 | 内容 |
|---|---|
| コマンド | `npm ci` / `npm run lint` / `npm test` / `npm run build` / `npm start` |
| Health | `GET /health/live`（プロセス生存）、`GET /health/ready`（依存先込み）。smoke testと台帳Health確認に使う |
| 設定 | 環境変数のみ。秘密値はservice設定またはGitHub Secrets |
| 待受 | `127.0.0.1:<PORT>`のみ。外部公開はIISが行う |
| log | 標準出力へJSON Lines。file化・rotationはservice wrapperが行う。Correlation IDを付与 |
| 停止 | SIGINT/SIGTERMで新規受付停止→処理完了後に終了 |
| 成果物 | CI（Windows runner）で`npm ci --omit=dev`まで実施しZIP化、SHA-256 checksum付与。本番serverで`npm install`しない |
| CI品質 | lint、test、`npm audit`、lockfile整合 |

既存アプリは契約部分（Health、設定、scripts、AI指示）だけを後付けすればよい。

### 2. 実行方式: アプリごとに Windows service + IIS reverse proxy

```text
D:\runtimes\node\v<version>\     Node本体（版ごとに並置、アプリごとに選択）
D:\apps\<code>\
   releases\<version>\           展開済み成果物
   current  →  releases\<version> ジャンクション
   config\                       環境変数ファイル等（ACL保護、releases外）
   logs\
```

- service化はWinSW（MIT）を候補とし、service名、実行account、自動再起動、log rotationをアプリごとに分離する。導入前に`docs/OSS_LICENSE_REVIEW.md`の手順で審査する。
- IISはアプリごとのsiteでTLS終端とhost名振り分けのみを担う（URL Rewrite + ARR）。DxPlatformの既存IIS構成は変更しない。
- Nodeはsystem共通installにせず、アプリごとに版を選ぶ。

### 3. デプロイ・ロールバック

| 操作 | 手順 |
|---|---|
| 起動・停止 | `Start-Service` / `Stop-Service <svc>`。runner accountには対象serviceの操作権限のみ個別付与し、管理者権限を与えない |
| デプロイ | artifact取得 → checksum検証 → `releases\<ver>`へ展開 → service停止 → ジャンクション切替 → 起動 → `/health/ready`確認 → 履歴登録（best-effort） |
| ロールバック | ジャンクションを直前releaseへ戻し再起動。deploy失敗時は自動実行し結果を`RolledBack`で登録 |
| 保持 | 直近N世代のreleaseを残し、古いものは運用jobで削除 |
| DB | アプリデプロイと分離した前方互換Migrationのみ。承認制（DxPlatformと同じ方針） |

### 4. 共通部品の配布

デプロイscriptとreusable workflowは共通repo（例: `dx-ops`）に置き、各アプリはtag固定で呼ぶ（`uses: <org>/dx-ops/.github/workflows/deploy-node.yml@v1`）。共通部品の変更は各アプリがtagを上げるまで影響しない。runner障害時のため、同じscriptを手動実行する手順をRunbookに記載する。

### 5. 採用しない方式

| 方式 | 理由 |
|---|---|
| iisnode | メンテナンス停止 |
| pm2 | Windows service化が非公式。全アプリを1プロセスマネージャが抱え共通障害点になる |
| HttpPlatformHandler / ANCM外部プロセス | 起動がIIS app poolに従属し切り分けにくい |
| NodeでHTTPS直接待受 | 証明書運用がアプリごとに必要 |

## Consequences

- アプリ単位で停止・更新・rollbackでき、他アプリへ影響しない。rollbackは数秒で完了する。
- IIS（ARR）とSelf-hosted runnerは共通依存として残る。IIS障害は現状のDxPlatformと同じ前提であり、runner障害はデプロイのみを止める。
- WinSW、ARRの導入とservice account・ACL設計が会社作業として増える。
- 既存アプリの移行は並行稼働（新port）→IISルール切替で1件ずつ行い、即時の方式変更はしない。
- native moduleは本番と同じOS/architectureのrunnerでbuildする必要がある。

## Evidence

- `scripts/deploy-iis.ps1`（copy方式、DxPlatform専用の成果物検査）
- `scripts/smoke-test.ps1`（`/health/ready`前提）
- `docs/DECISIONS/0004-github-actions-and-iis.md`
