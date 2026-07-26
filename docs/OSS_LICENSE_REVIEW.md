# OSS・配布ライセンス確認

会社持込み前に法務/OSS管理担当が `inventory/dotnet-dependencies.json`、Repository内のfrontend library LICENSE、GitHub dependency graphを確認する。

主な直接依存は Microsoft ASP.NET Core Identity / Entity Framework Core、xUnit、Microsoft.NET.Test.Sdk、coverletである。画面にはBootstrap、jQuery、jQuery Validationを同梱し、各libraryのLICENSE fileもpublish物に含まれる。

このRepositoryには現時点でプロジェクト本体の明示的なOSS licenseを付与していない。Public Repositoryで閲覧できることと、再配布・改変許諾は同義ではない。会社Repositoryへの取込み、社内配布、社外提供の可否とcopyright表記は、所有者と会社法務が正式決定する。判断前に任意のlicenseを推測して追加しない。

Releaseごとに次を記録する。

- direct/transitive dependency名・version・license・供給元
- 既知脆弱性、EOL、会社禁止packageの有無
- frontend source mapをProduction配布するか
- notice/license同梱要件
- SBOM形式と保管先
- review担当、日付、承認ticket
