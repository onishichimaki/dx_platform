# 実装計画

## 目的と優先順位

会社固有設定なしで起動・検証でき、会社では環境値と実データを差し替えて最終化できるMVPを構築する。優先順位は、(1)秘密情報を含まない安全な認証・認可、(2)資産台帳の一貫したCRUD、(3)運用・監査、(4)展開自動化、(5)引継ぎ可能な文書である。

## タスク、依存関係、完了条件

| 順序 | タスク | 主な依存 | 完了条件 |
|---:|---|---|---|
| 1 | Solution、4層Project、テストProject | .NET 10 SDK | restore/build成功、依存方向が規約どおり |
| 2 | Domain Entity、列挙型、EF Core設定、Migration | 1 | SQL Server MigrationとDevelopment SQLite起動 |
| 3 | Identity、初期ロール・管理者、Cookie、監査 | 2 | login/logout/change-password、無効ユーザー拒否 |
| 4 | ユーザー、グループ、ロール、アプリ権限 | 3 | 管理CRUD、拒否優先規則、直接URL拒否 |
| 5 | アプリ・データ・Agentカタログと関連 | 4 | 検索、詳細、登録、編集、無効化、関連表示 |
| 6 | ダッシュボード、Health、Deployment、監査、設定 | 5 | 数値・履歴表示、手動Health、認証済みDeployment API |
| 7 | Unit/Integration Test | 2–6 | 要求された主要シナリオが自動テストで成功 |
| 8 | CI/CD、IIS script | 6–7 | CI artifact、条件付きStaging、手動Production、rollback |
| 9 | 運用・会社引継ぎ・アーキテクチャ文書 | 1–8 | 必須文書とコードが一致し、根拠ファイルを記載 |
| 10 | 品質・Git確認 | 1–9 | restore/build/test/publish、秘密情報検査、意味あるcommit |

## テスト方法

- Domain/Application規則はxUnitの単体テストで網羅する。
- MVC、Identity、EF Core、Authorizationは`WebApplicationFactory`と一時SQLite DBで統合テストする。
- 外部HTTP Health Checkはテスト用Handlerを注入し、実ネットワークへアクセスしない。
- Release publish後にローカル起動と`/health/live`、`/health/ready`をSmoke Testする。

## リスクと対策

- ローカル端末に.NET 10がない: 公式`dotnet-install`で`.dotnet`へ非管理者導入し、`global.json`で固定する。
- SQL Server実機がない: SQL Server Migration生成は行い、機能テストはSQLiteで実施する。会社でMigration実行を検証する。
- SQLiteとSQL Serverの差異: DB固有SQLを避け、ProductionのSQLite起動を拒否する。
- IIS/Runnerへ接続できない: スクリプトの構文と引数を検証し、実配置は会社チェックリストへ明記する。
- 全画面の複雑化: 共通Controller/View部品と単純なMVCフォームで保守性を優先する。

## 会社でしかできない作業

SQL Server/IIS/証明書/サービスアカウント/会社GitHub/Environment/Runner/Secrets/通知/バックアップ/監視の設定、実データ投入、UAT、セキュリティ・ライセンス・本番移行承認は会社で行う。

