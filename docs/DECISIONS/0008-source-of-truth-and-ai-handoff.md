# ADR 0008: GitHub/SharePointの正本分担と、後任者向けAI開発指示・引継ぎ方式

- Status: Proposed（草案）
- Date: 2026-11-08
- Related: ADR 0005, ADR 0006, ADR 0007

## Context

完成目標は、担当者が異動しても、後任者がAI（Claude Code/Codex）と固定スクリプトを使って安全に改修・リリース・復旧できることである。そのためには次の状態が必要である。

- コードと業務資料の所在が明確である。
- AIが読む指示が各repoに揃っている。
- 固定スクリプトだけでリリース・復旧が完結する。
- 個人accountに依存しない。

前提:

- GitHubはソースコード・Issue・PRの管理に限定し、GitHub Actionsは使用しない（ADR 0006）。
- 開発PCと社内サーバーの間は、許可USBとウイルスチェック手順で成果物を受け渡す。
- DxPlatformでは`AGENTS.md`が開発規約を定め、社内URL等の会社固有情報をcommitしないルールがある。
- 社内ネットワーク内でAIを利用できるかは未確認である。

## Decision

### 1. 正本の分担

| 置き場所 | 置くもの |
|---|---|
| GitHub | コード、技術設計、ADR、Runbook（技術手順）、AI向け指示、固定スクリプト、変更Issue/PR、リリースtagとリリースIssue（version、commit、SHA-256、テスト結果の要約） |
| SharePoint | 業務要件、利用者マニュアル、リリース承認記録（version、ZIPのSHA-256、承認者）、問合せ先・体制表、業務影響を含む障害報告、実データ |
| DxPlatform台帳（DB） | 両者をつなぐ索引: GitHub repo URL、SharePoint folder URL（ADR 0005 変更B）、担当・副担当、稼働場所、デプロイ履歴 |
| 社内サーバー | 環境別設定・秘密値（ACL/DPAPI保護）、release世代、journal、log |

- 同じ文書を両方に置かない。片方を正本とし、他方は参照のみとする。
- 変更の流れは次のとおりとする。
  1. SharePointの要求
  2. GitHub Issue（要求IDで参照し、本文は転記しない）
  3. PR（ローカル検証結果の要約を記載）
  4. tagと成果物作成
  5. SharePoint承認
  6. USB持込み
  7. サーバーで固定スクリプトによるデプロイ
  8. journalとDxPlatform履歴への記録
- 社内URL、server名、環境別の実値はrepoに書かない。DxPlatform台帳（DB）またはサーバー上の設定ファイルに置く。
- 会社Organizationの業務アプリrepoにも、DxPlatformと同じ「会社固有情報禁止」ルールを適用するかは要決定とする。

### 2. AI向け指示

- 各repoの`AGENTS.md`を正本とする（Codexが読む）。`CLAUDE.md`には`@AGENTS.md`の読込と、Claude Code固有の事項だけを書く。
- DxPlatformの`AGENTS.md`の構成（目的、層の責務、コマンド、禁止事項、テスト必須、文書更新条件）を雛形とし、業務アプリ用に次を加える。
  - GitHub Actionsのworkflow（`.github/workflows/`）を追加しない。CI/CDサービスや外部runnerを前提にしない。
  - ビルド・テスト・成果物作成は固定スクリプト（verify/package）だけで行う。スクリプトの変更は通常の改修と同じくPRで行い、リリース作業中には編集しない。
  - AIはサーバー上の操作（deploy/rollback/start/stop）を実行しない。サーバー操作は、承認済みの担当者が固定スクリプトで行う。
  - 障害時は、Runbookとコードから原因を切り分けて対処案を提示し、必要に応じて修正版をPRで作る。rollbackは人が`rollback-app.ps1`で行う。
  - 社内から持ち出したlog等を扱う場合は、持出し可能な範囲（要確認）に限り、秘密値・個人情報を含めない。
- `.claude/settings.json`で次を拒否する。
  - サーバー操作スクリプトの実行
  - `.env`/`config\`の読取
  - `git push --force`
  - tagの削除・付け替え

### 3. 安全装置

- AIはbranchとPRでのみ作業する。PRのマージは人がレビューして行う。
- 成果物は、clean treeかつtagと一致するcommitからのみ作られる（ADR 0006）。AIが任意の状態の成果物を作れないようにする。
- 本番デプロイには、SharePoint承認記録の識別子（`-ApprovalReference`）と、承認記録上のSHA-256との一致を必須にする。
- AIにも開発者個人にも、サーバー管理者の認証情報やDeployment API tokenを持たせない。
- rollbackと状態確認は、AIなしで実行できる固定スクリプトとRunbookで完結させる。

### 4. Runbookの最低限の内容

各repoの`docs/RUNBOOK.md`に次を記載する。値はサーバー上の設定ファイルか台帳を参照する形にする。

- 開発PCでの手順: verify → package → リリースIssue記録 → 承認依頼
- 持込み手順: 会社のUSB・ウイルスチェック手順へのリンク（SharePoint）
- サーバーでの手順: verify-package → deploy → check-health → journalの確認
- rollback手順と、rollbackを判断する基準
- start/stop/status、logとjournalの場所
- 履歴の補完手順（`sync-deployment-journal.ps1`、`Unknown`の扱い）
- 典型的な障害と一次切り分け

### 5. 引継ぎ

各repoの`docs/HANDOFF.md`にチェックリストを置く。

- DxPlatform台帳のOwner・副担当を更新する。
- GitHub権限は個人でなくTeamで付与する。PRレビュー担当者を更新する。
- SharePointの承認者と権限を更新する。
- サーバーの運用アカウント・権限を更新する。個人しか知らない秘密値はrotateし、DPAPI保存し直す。service accountは個人に紐づけない。
- 開発PCを再構築し、固定スクリプトでverify/packageが成功することを確認する（SDK/Node版はrepoの版固定に従う）。
- 後任者がStagingでデプロイとrollbackを各1回、固定スクリプトで実施するまでを完了条件とする。
- DxPlatform Dashboardの「最終確認90日超過」警告を、文書の更新漏れの検知にも使う。

## Consequences

- 後任者は台帳から「コード・業務資料・稼働場所・担当・履歴」に辿れる。
- 手順はAIなしでも固定スクリプトとRunbookで完結し、AIは改修と原因切り分けを補助する役割になる。
- AIの変更はPRレビューと、tag・承認・hash照合のゲートで止まるため、本番への直接事故を防げる。その分、緊急時も人の承認とUSB持込みが必要になり、復旧の最短時間はrollback（既存releaseへの切替）に依存する。
- 各repoで文書を維持する負荷が生じる。PR本文のチェック項目と台帳の最終確認日で、更新漏れを抑える。

## Evidence

- `AGENTS.md`
- `docs/HANDOFF_TO_COMPANY.md`
- `docs/OPERATIONS.md`
- `src/DxPlatform.Web/Controllers/DashboardController.cs`（最終確認90日超過の警告）
