# Download packageの使い方

GitHubのReleasesから同じversionの次の4 fileをdownloadする。

| file | 用途 |
|---|---|
| `dx-platform-company-<version>.zip` | 手順書、設定template、app、Migration、script、inventoryを含む会社持込み用一式 |
| `dx-platform-iis-<version>.zip` | IIS deploy scriptへ渡すapp publish物 |
| `dxplatform-migrations-<version>.sql` | DBA review・適用用の冪等SQL |
| `SHA256SUMS.txt` | download fileの完全性確認 |

会社用ZIP内部:

```text
app/        IIS publish物（Development設定を除外）
config/     秘密値なしProduction template
database/   idempotent Migration SQL
docs/       会社構築、security、deployment、operations手順
inventory/  direct/transitive NuGet dependency JSON
scripts/    deploy、rollback、smoke、package検証
manifest.json
SHA256SUMS.txt
```

配布物には接続文字列、password、token、certificate、会社Host名、自宅PC名、DB fileを含めない。実値は会社側のsecret/configuration systemで入力する。`scripts/test-company-package.ps1`で内部checksumと必須fileを検証した後、`COMPANY_SETUP_GUIDE.md`の順序で構築する。

sourceから再生成する場合:

```powershell
.\scripts\build-company-package.ps1 `
  -Version 0.1.0-rc1 `
  -DotNetPath .\.dotnet\dotnet.exe
```

生成先は `artifacts/company/release`。`artifacts` はGit管理対象外である。
