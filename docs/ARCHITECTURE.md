# アーキテクチャ

## 概要

ASP.NET Core MVCとRazor Viewsによる単一デプロイのモジュラーモノリスを採用する。機能境界はレイヤーとフォルダで明確化しつつ、MVPで不要な分散システムの運用負荷を持ち込まない。

```text
Browser
  -> DxPlatform.Web (MVC, Authorization, Validation)
      -> DxPlatform.Application (use-case contracts, permission rules)
          -> DxPlatform.Domain (entities, enums, domain rules)
      -> DxPlatform.Infrastructure (EF Core, Identity, audit, health, seed)
          -> SQLite (Development/Test) / SQL Server (Production)
          -> approved HTTP/HTTPS health endpoints
```

## Project構成

```text
src/
  DxPlatform.Web/
  DxPlatform.Application/
  DxPlatform.Domain/
  DxPlatform.Infrastructure/
tests/
  DxPlatform.UnitTests/
  DxPlatform.IntegrationTests/
docs/
scripts/
.github/workflows/
```

依存方向は`Web -> Application/Infrastructure/Domain`、`Infrastructure -> Application/Domain`、`Application -> Domain`、`Domain -> なし`とする。WebはComposition Rootであり、ControllerはInfrastructureの`AppDbContext`を直接利用できるが、再利用する規則はApplicationサービスへ置く。

## 主要コンポーネント

- Identity: `ApplicationUser`、Cookie認証、Role、lockout、Security Stamp。
- Catalog: `ApplicationAsset`、`DataAsset`、`AgentAsset`、`ToolAsset`と関連テーブル。
- Authorization: アプリ権限評価サービスと管理Policy。拒否・個人・グループ・既定拒否の順序を固定する。
- Operations: `Deployment`、`HealthCheckResult`、`AuditLog`、安全な設定状態。
- Persistence: 単一`AppDbContext`、Entity Configuration、SQL Server Migration、Development SQLite。
- Cross-cutting: Correlation ID、構造化ログ、例外処理、監査サービス、UTC timestamp。

## データフロー

1. 認証Cookieを検証し、無効ユーザーをCookie eventで拒否する。
2. Controller/Policyがロールと対象資産権限を評価する。
3. MVC ViewModelの入力検証後、EF Coreでトランザクション保存する。
4. 変更成功/失敗を秘密情報のない要約として監査ログへ記録する。
5. 外部アプリ起動は保存済みURLを再検証し、HTTP/HTTPSだけを返す。

## 配置

GitHub ActionsでRelease publishをZIP化する。会社のWindows Self-hosted RunnerがIIS停止、バックアップ、配置、起動、Smoke Testを行い、失敗時に直前バックアップへ戻す。サーバー名・パス・tokenはRepositoryへ置かずVariables/Secretsから渡す。

## 変更容易性

対象Frameworkは共通MSBuild property、DB providerは設定とDI、ログ出力はProvider、将来の定期Health監視はinterfaceとして分離する。会社固有統合はInfrastructureへ追加し、Domain/Applicationへ環境詳細を漏らさない。

