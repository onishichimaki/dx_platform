# Roadmap

## MVP（本Repository）

Identity/role/user/group、App permission、App/Data/Agent/Tool catalogと関連、resource-filtered Dashboard/operations、SSRF強化済みmanual Health、Deployment API/history、Audit、safe settings、review用SQL Migration、Development SQLite、checksum付き会社package、CI/IIS deployment templateを提供する。

## 会社最終化

1. 会社承認済み.NET/SQL/IIS/OSS/license/security基準の確認。
2. 会社Repository、branch protection、Environment/Runner/Secrets、通知、backup/monitoring。
3. 実user/group/permission、demo削除、UAT、security review、production移行承認。

## 将来候補

- Entra ID/OIDC/MFAとaccount lifecycle同期。
- Health background schedulerと会社監視製品連携。
- GitHub metadata/deployment webhook連携、artifact署名検証。
- Health allowlistの集中管理・監査UIと会社監視製品連携。
- Data lineage、metadata自動収集、owner review workflow。
- Audit export/retention、通知、access review report。
- tag正規化UI、pagination、bulk import（安全な形式と承認を設計後）。

MVP非対象のAgent実行、LLM呼出、prompt本文、任意script/file upload、microservice/Kubernetesは、明確な承認とthreat modelなしに追加しない。
