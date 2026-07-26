# ADR 0001: モジュラーモノリスを採用する

- Status: Accepted
- Date: 2026-07-26

## Context

MVP は認証、資産台帳、権限、監査、運用を短期間で一貫して提供する必要がある。一方、将来の機能分割やテスト容易性も維持したい。

## Decision

単一プロセスの ASP.NET Core MVC アプリを、`Domain`、`Application`、`Infrastructure`、`Web` の4プロジェクトへ分離する。依存方向は Web → Infrastructure/Application、Infrastructure → Application/Domain、Application → Domain とし、Domain は他プロジェクトへ依存しない。

## Consequences

- 単一配備・単一トランザクションで MVP の運用負荷を抑えられる。
- 境界をプロジェクト参照とサービス抽象で保つため、将来の分離候補を識別できる。
- モジュールごとの独立スケールや独立リリースは行わない。

## Evidence

- `DxPlatform.slnx`
- `src/DxPlatform.Domain/DxPlatform.Domain.csproj`
- `src/DxPlatform.Application/DxPlatform.Application.csproj`
- `src/DxPlatform.Infrastructure/DxPlatform.Infrastructure.csproj`
- `src/DxPlatform.Web/DxPlatform.Web.csproj`
