# セキュリティ設計

## 実装済みcontrol

- Identity password hashing、unique email、lockout、security stamp、無効user拒否。
- Policy/role/resource level認可、既定拒否、直接URL再判定。
- MVC anti-forgery、Razor output encoding、DataAnnotations、EF Core parameter化query。
- HTTP/HTTPSだけを許可するURL validator。userinfo付きURL、javascript/data schemeを拒否。
- Health Checkはexact/wildcard-subdomain Host allowlist、Port allowlist、IP分類を検証する。loopback、link-local、metadata/benchmark/multicastを常時拒否し、private addressは明示設定時だけ許可。
- Health Check接続時にもDNSを再解決して許可IPへ直接接続し、HTTP redirectとcookieを拒否する。
- Dashboard、Deployment履歴、Health実行・履歴をApp resource権限でfilterし、AppOwnerは所有Appだけを管理する。
- CSP、frame拒否、MIME sniff拒否、referrer/permissions policyを全responseへ付与する。
- loginとDeployment APIにIP単位のrate limitを付与する。
- ProductionのHTTPS redirect、HSTS、Secure/HttpOnly/SameSite cookie、developer exception無効。
- ProductionでSQLiteまたは接続文字列未設定なら起動失敗。
- Productionは`AllowedHosts=*`を拒否し、未適用Migrationがあれば起動を停止する。schema自動更新は既定無効。
- Productionは絶対pathのData Protection key ringを必須とし、Windows DPAPIでapp pool identityに保護する。
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
- SSRF: allowlist、private IP opt-in、接続時再検査、redirect拒否、timeout/cooldownで軽減。会社では正確なHost/Portとegress Firewallを設定する。
- XSS: Razor encodeと入力長。external linkは`noopener noreferrer`。
- SQL injection: LINQ/EF parameter化。任意SQL機能なし。
- deployment改ざん: GitHub Environment approval、protected branch、self-hosted Runner管理、artifact/commit SHAで軽減。署名artifact検証は将来課題。

security incident時はtoken/password/certificateを先にrotateし、audit/log、GitHub Actions、IIS、SQL Server access logをCorrelation ID/commit/run IDで照合する。
