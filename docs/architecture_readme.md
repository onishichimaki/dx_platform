# Architecture documentation guide

このフォルダーの3ファイルは、同じ現行アーキテクチャを異なる用途で表現する。

- `architecture.html`: 人が全体像、画面/API/DB、CI/CD、外部連携、リスクを対話的に確認する自己完結ページ。
- `architecture.json`: AI や検査スクリプトが利用する機械可読 inventory。各要素の `source_files` が根拠を示す。
- `architecture_readme.md`: 読み方と更新手順（このファイル）。

## 読み方

まず `architecture.html` の「全体」で依存方向と主要フローを確認し、ノードを選んで右側の根拠ファイルを開く。詳細調査や次の AI への引継ぎでは `architecture.json` の安定 ID と `source_files` を利用する。設計意図は `DECISIONS` 配下の ADR、運用詳細は `ARCHITECTURE.md`、`SECURITY.md`、`OPERATIONS.md`、`DEPLOYMENT.md` を参照する。

## 更新手順

1. 変更された entrypoint、controller、entity、migration、workflow、script を確認する。
2. `architecture.json` の該当 inventory、flow、risk と `source_files` を更新する。
3. `architecture.html` の表示データと図を同期する。
4. 未確認事項を断定せず `risks_or_unknowns` に追加する。
5. JSON、HTML、source evidence の検証を実行する。

## Discovery commands

```powershell
rg --files
rg -n "class .*Controller|\[Route|MapHealthChecks" src\DxPlatform.Web
rg -n "DbSet<|HasOne|HasMany|ToTable" src\DxPlatform.Infrastructure\Persistence
rg -n "uses:|dotnet |deploy-iis" .github scripts
python -m json.tool docs\architecture.json > $null
```

アーキテクチャ上の不明点や会社環境でのみ確定できる事項は、必ず `risks_or_unknowns` に残す。
