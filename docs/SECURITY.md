# セキュリティ設計

## 実装済みcontrol

- Identity password hashing、unique email、lockout、security stamp、無効user拒否。
- Policy/role/resource level認可、既定拒否、直接URL再判定。
- MVC anti-forgery、Razor output encoding、DataAnnotations、EF Core parameter化query。
- HTTP/HTTPSだけを許可するURL validator。userinfo付きURL、javascript/data schemeを拒否。
- ProductionのHTTPS redirect、HSTS、Secure/HttpOnly/SameSite cookie、developer exception無効。
- ProductionでSQLiteまたは接続文字列未設定なら起動失敗。
- Correlation IDを安全な文字と長さに制限し、response headerとlog scopeへ付与。
- Deployment API tokenを設定file/DB/logへ保存せず、SHA-256 hashを固定時間比較。
- ConnectionReferenceへconnection string/passwordらしい値を拒否。
- manual Health Checkはtimeout、response body非取得、error要約、30秒cooldown。
- auditはpassword/token/cookie/connection string/input/API response全文を保存しない。
- 任意code/PowerShell/file upload/deployment script登録機能を持たない。

## 秘密情報管理

RepositoryへProduction connection、certificate、private key、token、password、会社固有値を登録しない。環境変数またはUser Secretsを優先し、会社ではGitHub Secrets/IIS環境変数/承認済みsecret storeを使う。`appsettings.Example.json`の値はplaceholderである。

秘密scanは最終確認でtracked fileに対して実施する。GitHub secret scanning、Dependabot、branch protectionの有効化は会社/remote側作業である。

## ログ・監査

Consoleと日次JSON line file providerを用意し、Timestamp、level、event、category、message、CorrelationId、UserId、RequestPathと明示scopeを記録できる。file write失敗は業務処理を停止させない。ProductionではIIS folder ACL、保存期間、rotation/collectorを会社運用で設定する。

監査要約は操作と対象ID中心で、userのemail/passwordや入力全文を含めない。Deployment notes/log referenceは代表的なsecret markerを拒否するが、自動DLPではないためcaller側のredactionも必須である。

## Threatと残余risk

- internal account漏えい: strong password、lockout、最小role、定期棚卸しで軽減。MFA/Entra IDは将来範囲。
- SSRF: Health URLはadmin登録、scheme制限、manual実行、timeout/cooldown。ただしhost allowlistは会社network確定後に追加が必要。
- XSS: Razor encodeと入力長。external linkは`noopener noreferrer`。
- SQL injection: LINQ/EF parameter化。任意SQL機能なし。
- deployment改ざん: GitHub Environment approval、protected branch、self-hosted Runner管理、artifact/commit SHAで軽減。署名artifact検証は将来課題。

security incident時はtoken/password/certificateを先にrotateし、audit/log、GitHub Actions、IIS、SQL Server access logをCorrelation ID/commit/run IDで照合する。

