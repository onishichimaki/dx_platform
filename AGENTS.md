# DX共通アプリ基盤 開発ガイド

## 目的

本リポジトリは、社内DX資産の共通入口と管理台帳を提供するASP.NET Core MVCのモジュラーモノリスである。巨大な統合ランタイム、任意スクリプト実行基盤、外部Agent実行基盤にはしない。

## アーキテクチャとレイヤー責務

- `DxPlatform.Domain`: エンティティ、列挙型、ドメイン規則。Web、EF Core、外部サービスへ依存しない。
- `DxPlatform.Application`: ユースケース用DTO、サービスインターフェース、権限判定などのアプリケーション規則。Domainのみに依存する。
- `DxPlatform.Infrastructure`: EF Core、Identity、監査、ヘルスチェック、Seed、外部連携の実装。ApplicationとDomainに依存する。
- `DxPlatform.Web`: MVC Controller、ViewModel、Razor View、認可、入力検証、HTTP境界。Application、Infrastructure、DomainをComposition Rootで組み立てる。
- 過度なRepository、CQRS、イベント駆動、マイクロサービスは導入しない。EF Coreの`DbContext`を単一トランザクション境界として扱う。

## コーディング・命名規約

- C#はnullable reference typesとimplicit usingsを有効にする。
- 公開型・公開メンバーはPascalCase、ローカル変数・引数はcamelCase、非公開フィールドは`_camelCase`とする。
- 非同期I/Oは`Async`接尾辞を付け、`CancellationToken`を伝播する。
- Controllerを薄く保ち、権限規則と再利用する処理をサービスへ置く。
- 文字列長、必須、URL、列挙値をHTTP境界とDB制約の両方で検証する。
- 日時はUTCで保存し、表示時にローカライズする。
- 主要マスタは`RowVersion`による楽観的排他、原則`IsActive`による論理削除を使用する。

## セキュリティ規約

- 認証対象Controllerは匿名公開しない。匿名許可はログイン、アクセス拒否、基盤ヘルスチェックに限定する。
- 管理画面はControllerのPolicy/RoleとViewの表示条件の両方で保護する。
- アプリ詳細・起動は必ずサーバー側のアプリ権限を再判定する。
- 状態変更はPOSTとAnti-forgery tokenを使う。外部URLはHTTP/HTTPSのみ許可する。
- パスワード、Token、Cookie、接続文字列、秘密鍵、入力・API応答の全文をログや監査ログへ記録しない。
- 初期管理者とDeployment API tokenは環境変数またはUser Secretsから取得する。
- ProductionでSQLiteを選択した場合は起動を拒否し、HTTPS/HSTSと安全なCookieを使用する。
- 会社名、社内URL、IP、サーバー・DB・ユーザー・顧客・製品等の実情報をコミットしない。Seedは架空名のみとする。

## テスト必須条件

- 権限の拒否優先順位、ロール、無効ユーザー、URL検証、ステータス遷移、必須項目を単体テストする。
- 認証、管理画面認可、アプリ直接URL拒否、主要CRUD、監査、ヘルスチェックを統合テストする。
- 不具合修正には再現テストを追加する。
- 完了前に次を実行し、すべて成功させる。

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet publish src/DxPlatform.Web/DxPlatform.Web.csproj -c Release --no-restore
```

ローカルに固定SDKがない場合は`.\.dotnet\dotnet.exe`を使用する。

## 禁止事項

- 任意コード、PowerShell、デプロイスクリプトの登録・実行機能を追加しない。
- 秘密情報、会社固有情報、本物のSystem Prompt、APIキーを保存しない。
- Production用接続文字列を設定ファイルへ書かない。
- 認可をViewの非表示だけで済ませない。
- テスト失敗、コンパイルエラー、未検証のダミー実装を完成扱いにしない。
- 既存仕様、認可優先順位、対象フレームワークを無断で変更しない。

## ドキュメント更新条件

- Entityや関連を変えたら`docs/DATA_MODEL.md`とMigrationを更新する。
- Route/API/認可を変えたら`docs/REQUIREMENTS.md`、`docs/AUTHORIZATION_RULES.md`、`docs/architecture.*`を更新する。
- 設定、運用、CI/CDを変えたらREADME、`docs/OPERATIONS.md`、`docs/DEPLOYMENT.md`、会社引継ぎ文書を更新する。
- 重要な設計判断は`docs/DECISIONS/`へADRを追加する。
- 軽微な不明点は`docs/ASSUMPTIONS.md`へ明記し、会社固有値を推測しない。

