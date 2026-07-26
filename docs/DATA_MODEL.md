# データモデル

## 共通方針

主要マスタは `Guid Id`、UTCの作成/更新日時、作成/更新者、`IsActive`、16 byteの`RowVersion`を持つ。削除は原則論理削除で、更新時にRowVersionを変更し楽観的排他を行う。SQL Server Migrationでは列、外部キー、unique制約、check制約、検索indexを定義する。

SQLiteは`DateTimeOffset`のORDER BY制約を回避するためDevelopment/Test modelだけUTC ticksへ変換する。SQL Server modelは`datetimeoffset`を維持する。

## Entity一覧

| Entity | 主な責務・制約 |
|---|---|
| `ApplicationUser` | Identity user、表示名、所属、有効状態、最終login。email unique、無効userはCookie検証でも拒否 |
| `ApplicationRole` | Identity role。SystemAdmin/CatalogAdmin/AppOwner/Viewer。共通監査列とIdentity concurrency stampを持つ |
| `Group` | code unique、名称、説明、有効状態 |
| `GroupMember` | GroupとUserの中間。`GroupId + UserId` unique |
| `ApplicationAsset` | code unique、URL、種別、stack、責任者、環境、状態、version、health URL、機密区分等 |
| `ApplicationPermission` | AppとUserまたはGroupの一方だけを参照。principalごとunique、None/View/Use/Manage |
| `DataAsset` | 種別、system名、owner、更新頻度、機密区分、保持期間、接続先参照名等 |
| `AgentAsset` | 種別、platform、用途、model参照、prompt metadata、書込/外部送信、risk、評価等 |
| `ToolAsset` | Agentが利用するTool/APIの名称、種別、安全な参照URL。認証情報は持たない |
| `ApplicationDataAsset` | AppとDataの多対多 |
| `AgentDataAsset` | AgentとDataの多対多 |
| `AgentToolAsset` | AgentとTool/APIの多対多 |
| `DataAssetRelation` | Data同士の関連。自己参照と重複を画面・unique制約で制御 |
| `Deployment` | App、version、commit、branch、actor、開始/終了、結果、環境、rollback元、log参照 |
| `HealthCheckResult` | App、成功/失敗/timeout、HTTP status、応答ms、確認日時、安全なerror要約 |
| `AuditLog` | 操作日時、actor、操作/対象、成否、安全な要約、Correlation ID。append-only運用 |
| `Tag` | 正規化tag master。name unique |
| `AssetTag` | AssetKindとAssetIdを使う汎用tag関連。MVP画面はTagsTextも検索用に保持 |

## 主要関連

```text
ApplicationUser 1---* GroupMember *---1 Group
ApplicationAsset 1---* ApplicationPermission *---0..1 ApplicationUser
ApplicationAsset 1---* ApplicationPermission *---0..1 Group
ApplicationAsset *---* DataAsset (ApplicationDataAsset)
AgentAsset       *---* DataAsset (AgentDataAsset)
AgentAsset       *---* ToolAsset (AgentToolAsset)
DataAsset        *---* DataAsset (DataAssetRelation)
ApplicationAsset 1---* Deployment
ApplicationAsset 1---* HealthCheckResult
Tag              1---* AssetTag
```

`ApplicationPermission`のcheck制約はUserId/GroupIdのどちらか一方だけが設定されることを保証する。Userの`None`は明示拒否であり、物理削除とは異なる。権限解除はレコードを無効化する。

## Migration

SQL Server Migrationsは `src/DxPlatform.Infrastructure/Persistence/Migrations` にある。Design-time factoryの接続文字列はschema生成専用の架空local placeholderで、実DBへ接続しない。適用時はCLIの`--connection`またはProduction環境変数から実接続を渡す。
