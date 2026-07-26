# 認証・認可規則

## 認証

ASP.NET Core Identityのapplication cookieを使う。passwordはIdentity hasherで保存し、5回失敗で15分lockoutする。無効userはpassword確認前と既存cookieの再検証時の両方で拒否する。Production/通常DevelopmentはHTTPS、`__Host-` prefix、Secure、HttpOnly、SameSite=Laxを使用する。

## ロール

- `SystemAdmin`: 全管理、全AppのManage、user/group/permission/audit/settings。
- `CatalogAdmin`: App/Data/Agent/Toolの登録・編集・無効化。
- `AppOwner`: 自分がownerのAppを編集し、利用状況を確認。
- `Viewer`: 許可されたAppの閲覧・起動、一般Data/Agent catalogの閲覧。

Controller policyを必須とし、Viewのmenu/ボタン非表示は補助に過ぎない。Appの対象resourceはControllerで毎回再判定する。

## App権限評価

1. SystemAdminなら`Manage`。
2. user個別`None`があれば拒否。groupのManageより優先。
3. user個別許可があればそのlevel。
4. user個別設定がなければ、有効groupの最大level。
5. 設定なしは`None`。

包含関係は `Manage >= Use >= View > None`。`View`で一覧/詳細、`Use`で外部起動、`Manage`で編集・無効化できる。AppOwnerはowner登録されたAppもManage相当で編集できる。

## 一覧と直接URL

`/apps`は評価結果がView以上のIDだけをqueryする。`/apps/{id}`と`/apps/{id}/launch`はID取得後に再評価し、権限不足ならForbidする。したがって一覧非表示だけではなくURL直打ちも拒否する。無効Appは一般routeではNotFoundになる。

## 管理route

| route | 規則 |
|---|---|
| `/admin/users`, `/admin/groups`, `/admin/permissions`, `/audit-logs`, `/settings` | SystemAdmin |
| App/Data/Agentのcreate | SystemAdminまたはCatalogAdmin |
| App edit/disable | SystemAdmin、CatalogAdmin、owner user、またはManage permission |
| Data/Agent/Tool edit | SystemAdminまたはCatalogAdmin |
| `/admin/health-checks` | SystemAdmin、CatalogAdmin、AppOwner |
| `POST /api/deployments` | Identity cookieではなく専用Bearer token |

すべての状態変更formはPOSTとanti-forgery tokenを使用する。Deployment APIはmachine-to-machine endpointのためCSRF対象外だが、設定済みtokenの固定時間hash比較を必須とする。

