# ADR 0002: ASP.NET Core Identity による内部認証を採用する

- Status: Accepted
- Date: 2026-07-26

## Context

MVP では社内 IdP の具体的なテナント、アプリ登録、クレーム仕様が未確定である。認証・ロール・無効化・パスワード変更・監査を先に成立させる必要がある。

## Decision

ASP.NET Core Identity の Cookie 認証を採用し、SystemAdmin、CatalogAdmin、AppOwner、Viewer の固定ロールを用いる。Cookie は HttpOnly、Secure、SameSite=Lax とし、無効化されたユーザーの既存セッションも principal 検証時に拒否する。将来の Entra ID 連携は認証境界の差し替えとして扱う。

## Consequences

- 外部 IdP なしで開発・評価できる。
- パスワードポリシー、ロックアウト、ユーザー無効化をアプリ側で運用する必要がある。
- 本番移行前に会社の SSO/MFA 方針との整合確認が必要である。

## Evidence

- `src/DxPlatform.Infrastructure/DependencyInjection.cs`
- `src/DxPlatform.Infrastructure/Identity/ApplicationIdentity.cs`
- `src/DxPlatform.Web/Program.cs`
- `src/DxPlatform.Web/Controllers/AccountController.cs`
