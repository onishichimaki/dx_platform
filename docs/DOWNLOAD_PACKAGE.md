# Download packageの使い方

開発PCで `scripts/release-local.ps1` を実行すると、`artifacts/company/release/` に同じversionの次のfileが作られる。承認記録の値と照合したうえで、許可されたUSBと会社のmalware scan手順で会社端末へ持ち込む（GitHub Releaseからのdownloadは使用しない）。

| file | 用途 |
|---|---|
| `dx-platform-company-<version>.zip` | 手順書、設定template、app、Migration、script、inventoryを含む会社持込み用一式 |
| `dx-platform-iis-<version>.zip` | IIS deploy scriptへ渡すapp publish物 |
| `dxplatform-migrations-<version>.sql` | DBA review・適用用の冪等SQL |
| `SHA256SUMS.txt` | 持込みfileの破損検知（改ざん検知は承認記録のSHA-256との照合で行う） |
| `RELEASE_RECORD.md` / `release-record.json` | version、source commit、テスト・脆弱性監査・Migration差分の結果、各fileのSHA-256。承認記録へ転記する |

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

配布物には接続文字列、password、token、certificate、会社Host名、自宅PC名、DB fileを含めない。実値は会社側のsecret/configuration systemで入力する。`scripts/test-company-package.ps1 -RequireReleasable`で内部checksum、必須file、検証証跡を確認した後、`COMPANY_SETUP_GUIDE.md`の順序で構築する。

sourceから再生成する場合:

```powershell
git tag -a v<version> -m "release <version>"
.\scripts\release-local.ps1 -Version <version>
```

検証（テスト、NuGet監査、Migration差分）が失敗した場合は成果物は作られない。生成先は `artifacts/company/release`。`artifacts` はGit管理対象外である。
