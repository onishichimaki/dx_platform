# 要件定義

## システム境界

本システムはDX資産を実行する統合ランタイムではなく、共通入口、カタログ、権限、稼働・展開・監査履歴を管理するモジュラーモノリスである。任意コード実行、Agent実行、ファイルアップロード、社内認証連携、自動メタデータ収集はMVP対象外とする。

## 利用者とロール

- `SystemAdmin`: 全管理、ユーザー・グループ・権限・監査・設定。
- `CatalogAdmin`: アプリ、データ、Agentの登録・編集。
- `AppOwner`: 所有またはManage権限のあるアプリの編集と利用状況確認。
- `Viewer`: 許可されたアプリと一般カタログの閲覧。

## 必須機能

1. Identityによるログイン、ログアウト、パスワード変更、ロックアウト、無効ユーザー拒否、環境変数による初期管理者。
2. ユーザー検索・登録・編集・無効化・ロール・グループ所属管理。
3. グループ検索・登録・編集・メンバー管理・無効化。
4. アプリ検索・絞込み・詳細・登録・編集・無効化・安全な外部起動。
5. ユーザー/グループの`None/View/Use/Manage`権限と、ユーザー拒否優先規則。
6. データ資産とAgentの検索・詳細・登録・編集・無効化、およびアプリ/Agent/Tool等との関連表示。
7. KPI、警告、直近Deployment、直近管理操作を示すダッシュボード。
8. 管理者によるHTTP Health Checkと基盤自身の`/health/live`、`/health/ready`。
9. Deployment履歴一覧と認証済み登録API。
10. 指定された重要操作の安全な要約を持つ監査ログ。
11. ProductionのHTTPS/HSTS、SQLite拒否、例外詳細非表示、セキュアCookie。

## 画面とAPI

| Route | 用途 | 主な認可 |
|---|---|---|
| `/login` | ログイン | 匿名 |
| `/dashboard` | KPI・履歴 | 認証済み |
| `/apps`, `/apps/{id}` | 許可されたアプリ | アプリView以上 |
| `/admin/apps` | アプリ管理 | Catalog管理またはAppOwner |
| `/admin/users` | ユーザー管理 | SystemAdmin |
| `/admin/groups` | グループ管理 | SystemAdmin |
| `/admin/permissions` | アプリ権限 | SystemAdmin |
| `/data-assets`, `/data-assets/{id}` | データカタログ | 認証済み |
| `/agents`, `/agents/{id}` | Agentカタログ | 認証済み |
| `/deployments` | Deployment履歴 | 認証済み |
| `/audit-logs` | 監査ログ | SystemAdmin |
| `/settings` | 安全な設定状態 | SystemAdmin |
| `/api/deployments` | Deployment登録 | 専用Bearer token |
| `/health/live`, `/health/ready` | 基盤Health | 匿名、秘密非表示 |

## 非機能要件

- Windows Server/IIS/SQL Serverを正式環境、Development SQLiteをデモ環境とする。
- CSRF、XSS、SQL Injection、Cookie、HTTPS、最小権限、秘密情報非記録に対応する。
- UTC保存、構造化ログ、Correlation ID、楽観的排他、論理削除を採用する。
- .NET 10対象を一箇所で変更しやすくし、会社互換性事項をチェックリスト化する。
- Restore/Build/Unit/Integration/PublishをCIで実行し成果物と試験結果を保存する。

