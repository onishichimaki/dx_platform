# ADR 0004: GitHub Actions と IIS 配備を採用する

- Status: Accepted
- Date: 2026-07-26

## Context

Windows Server / IIS を想定した再現可能な build、test、package、deploy、smoke test、rollback が必要である。会社固有の runner、site、path、URL は未確定である。

## Decision

GitHub Actions で CI と staging/production 配備 workflow を分離する。CI は GitHub-hosted Windows runner、配備は会社管理の self-hosted Windows runner を利用する。配備対象値は Environment variables/secrets で注入し、ZIP 展開前のバックアップ、`/health/ready` smoke test、失敗時 rollback を PowerShell で実施する。本番は手動起動と Environment approval を前提とする。

## Consequences

- 会社固有値をコードへ含めず、同一 artifact を昇格できる。
- self-hosted runner、IIS 権限、Environment protection の構築が引継ぎ事項になる。
- 初回配備は rollback 元がないため、失敗時は site/app pool を停止する。

## Evidence

- `.github/workflows/ci.yml`
- `.github/workflows/deploy-staging.yml`
- `.github/workflows/deploy-production.yml`
- `scripts/deploy-iis.ps1`
- `scripts/rollback-iis.ps1`
- `scripts/smoke-test.ps1`
