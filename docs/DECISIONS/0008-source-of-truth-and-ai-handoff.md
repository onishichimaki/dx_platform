# ADR 0008: GitHub/SharePointの正本分担と、後任者向けAI開発指示・引継ぎ方式

- Status: Proposed（草案）
- Date: 2026-11-08
- Related: ADR 0005, ADR 0006

## Context

開発者が異動しても、後任者がClaude Code/Codexを使って安全に開発・改修・デプロイ・障害対応できる必要がある。そのためには、コードと業務資料の所在が明確で、AIが読む指示が各repoに揃い、個人accountへの依存がない状態にする必要がある。DxPlatformでは`AGENTS.md`が開発規約を定め、社内URL等の会社固有情報をcommitしないルールがある。

## Decision

### 1. 正本の分担

| 置き場所 | 置くもの |
|---|---|
| GitHub | コード、技術設計、ADR、Runbook（技術手順）、AI向け指示、デプロイ定義、変更Issue/PR |
| SharePoint | 業務要件、利用者マニュアル、承認記録、問合せ先・体制表、業務影響を含む障害報告、実データ |
| DxPlatform台帳（DB） | 両者をつなぐ索引: GitHub repo URL、SharePoint folder URL（ADR 0005 変更B）、担当・副担当、稼働場所 |

- 同じ文書を両方に置かない。片方を正本とし、他方は参照のみとする。
- 変更の流れは「SharePoint要求 → GitHub Issue（要求IDで参照し本文は転記しない）→ PR → デプロイ → DxPlatform履歴」とする。
- 社内URLや環境別実値はrepoに書かず、DxPlatform台帳（DB）またはGitHub Variablesに置く。
- 会社Organizationの業務アプリrepoにもDxPlatformと同じ「会社固有情報禁止」ルールを適用するかは要決定とする（推奨: 環境別実値はVariablesへ置く）。

### 2. AI向け指示

- 各repoの`AGENTS.md`を正本とする（Codexが読む）。`CLAUDE.md`は`@AGENTS.md`の読込とClaude Code固有事項のみとする。
- DxPlatformの`AGENTS.md`の構成（目的、層の責務、コマンド、禁止事項、テスト必須、文書更新条件）を雛形とし、業務アプリ用に次を加える。
  - 本番デプロイはGitHub Actions経由のみ。AIはserverへ直接操作しない。
  - 障害時はRunbookを読み、原因切り分けと対処案の提示までを行う。rollback実行は人がworkflowから行う。
- `.claude/settings.json`で本番向けコマンドや`.env`/`config\`の読取を拒否する。

### 3. 安全装置

- AIはbranchとPRでのみ作業する。
- 本番デプロイにはGitHub Environmentでの人の承認を必須とする。
- AIにも開発者個人にもserver管理者の認証情報を持たせない。
- rollbackはworkflow化し、人が手動実行できるようにする。

### 4. 引継ぎ

各repoの`docs/HANDOFF.md`にチェックリストを置く。

- DxPlatform台帳のOwner・副担当を更新する。
- GitHub権限は個人でなくTeamで付与し、Environment reviewerを更新する。
- 個人しか知らない秘密値はrotateする。service accountは個人に紐づけない。
- SharePointの権限を更新する。
- 後任者がStagingでデプロイとrollbackを各1回実施するまでを完了条件とする。
- DxPlatform Dashboardの「最終確認90日超過」警告を文書更新漏れの検知にも使う。

## Consequences

- 後任者は台帳から「コード・業務資料・稼働場所・担当」に辿れる。
- AIの作業はPRと承認ゲートで止まるため、本番への直接事故を防げる。代わりに緊急時も人の承認が必要になる。
- 各repoに文書を維持する負荷が生じる。PRテンプレートのチェック項目と台帳の最終確認日で更新漏れを抑える。

## Evidence

- `AGENTS.md`
- `docs/HANDOFF_TO_COMPANY.md`
- `docs/OPERATIONS.md`
- `src/DxPlatform.Web/Controllers/DashboardController.cs`（最終確認超過の警告）
