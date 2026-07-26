# 既知の制限

- external認証（AD/Entra/OIDC/SAML/LDAP）とMFAは未実装。MVPはinternal Identity password。
- SQLiteはDevelopment/Test専用。SQL Server固有のperformance、collation、HA、backupは会社環境で追加検証する。
- background定期Health監視はinterfaceのみ。manual実行と基盤Health endpointを提供し、正式監視は外部製品を使う。
- Health URLはscheme/timeout/cooldownを制御するが、network/host allowlistは会社network確定後に追加する。
- App以外のData/Agent catalogはMVPでは認証user共通閲覧。機密区分別ABACは将来課題。
- tagは検索用textとEntityを持つが、正規化tag管理UIは未完成。
- pagination/bulk import/export、自動metadata crawl、完全data lineageは未実装。件数増加前にquery/profileを行う。
- Agent実行、LLM呼出、System Prompt本文、credential、file upload、任意scriptは意図的に対象外。
- Deploymentはfile rollbackを自動化するが、DB Migration rollbackは自動化しない。
- Deployment APIは共有Bearer token方式。rotation/複数client/short-lived credentialは会社基盤に合わせて拡張する。
- 初回無効化後の再有効化専用confirmation UIは未実装。必要時は承認済み管理手順または将来UIで行う。
- file loggerは単一node向けの簡易JSON Lines。正式なrotation/central collectionは会社側で構成する。
- accessibilityは基本focus/label/responsive/color非依存を実装したが、専門的WCAG監査は未実施。
- 会社標準SQL Server、IIS、Production certificate、Runnerを使うend-to-end deployは会社環境でのみ完了できる。開発用SQL ServerではMigrationsとreadinessまで検証済み。
