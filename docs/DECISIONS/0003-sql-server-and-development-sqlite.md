# ADR 0003: 本番 SQL Server、開発・テスト SQLite を採用する

- Status: Accepted
- Date: 2026-07-26

## Context

本番は SQL Server が要件である一方、開発者と CI は追加インフラなしに再現可能であることが望ましい。

## Decision

EF Core の同一 `AppDbContext` に SQL Server と SQLite の provider を設定する。本番で SQLite が選択された場合は起動を失敗させる。SQL Server は migration、SQLite は開発・テスト用 `EnsureCreated` を利用する。接続文字列は環境変数または Secret Store から注入し、リポジトリへ保存しない。

## Consequences

- 開発・CI の立ち上げが簡単になる。
- provider 差異があるため SQL Server migration と実機適用をリリース判定に含める。
- SQLite の DateTimeOffset 比較には UTC ticks 変換を適用する。

## Evidence

- `src/DxPlatform.Infrastructure/DependencyInjection.cs`
- `src/DxPlatform.Infrastructure/Persistence/AppDbContext.cs`
- `src/DxPlatform.Infrastructure/Persistence/DatabaseInitializer.cs`
- `src/DxPlatform.Infrastructure/Persistence/Migrations/20260726090428_InitialSqlServer.cs`
- `src/DxPlatform.Infrastructure/Persistence/Migrations/20260726093846_AddIdentityAuditMetadata.cs`
