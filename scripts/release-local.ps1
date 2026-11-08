<#
.SYNOPSIS
  開発PCでリリース成果物を作成する（GitHub Actionsは使用しない）。

.DESCRIPTION
  次を順に実行し、1つでも失敗した場合は成果物を確定させず終了コード1を返す。
    1. HEADにタグ v<Version> が付いていること、作業ツリーがcleanであること
    2. verify-local.ps1（NuGet監査、build、全テスト、Migration差分）
    3. build-company-package.ps1（検証証跡の同梱、SHA-256、リリース記録の作成）
    4. 作成したZIPを展開し直して test-company-package.ps1 で検証
    5. release/SHA256SUMS.txt と実ファイルのSHA-256を再照合
  成果物は artifacts/company/release/ に出力する。RELEASE_RECORD.md の内容を
  GitHubのリリースIssueとSharePointの承認記録へ転記し、承認後に許可USB手順で社内へ持ち込む。
  このスクリプトはネットワークへの公開（push、Release作成、アップロード）を行わない。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]{0,63}$')]
    [string]$Version,

    [string]$DotNetPath
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$extractPath = Join-Path ([System.IO.Path]::GetTempPath()) "dxplatform-release-check-$([guid]::NewGuid().ToString('N'))"

function Assert-ExitCode {
    param([string]$Step)
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed (exit $LASTEXITCODE)."
    }
}

Push-Location $repositoryRoot
try {
    $tag = "v$Version"
    if ((git tag --points-at HEAD) -notcontains $tag) {
        throw "HEAD does not have the release tag $tag. Create it on the commit to release (git tag -a $tag -m '...') and run again."
    }
    if (git status --porcelain) {
        throw 'Working tree is dirty. Commit the exact release source first.'
    }

    $common = @{}
    if (-not [string]::IsNullOrWhiteSpace($DotNetPath)) { $common.DotNetPath = $DotNetPath }

    Write-Host '=== 1/4 verify-local ===' -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'verify-local.ps1') @common
    Assert-ExitCode 'verify-local.ps1'

    Write-Host '=== 2/4 build-company-package ===' -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot 'build-company-package.ps1') -Version $Version -NoRestore -RequireTag @common
    Assert-ExitCode 'build-company-package.ps1'

    $releaseDirectory = Join-Path $repositoryRoot 'artifacts/company/release'
    $companyZip = Join-Path $releaseDirectory "dx-platform-company-$Version.zip"

    Write-Host '=== 3/4 test-company-package (extracted ZIP) ===' -ForegroundColor Cyan
    Expand-Archive -LiteralPath $companyZip -DestinationPath $extractPath -Force
    & (Join-Path $PSScriptRoot 'test-company-package.ps1') -PackageRoot $extractPath
    Assert-ExitCode 'test-company-package.ps1'
    $manifest = Get-Content -LiteralPath (Join-Path $extractPath 'manifest.json') -Raw | ConvertFrom-Json
    $head = (git rev-parse HEAD).Trim()
    if ($manifest.sourceCommit -ne $head -or $manifest.releasable -ne $true) {
        throw 'The package manifest does not match HEAD or is not releasable.'
    }

    Write-Host '=== 4/4 release checksums ===' -ForegroundColor Cyan
    $mismatches = foreach ($line in Get-Content -LiteralPath (Join-Path $releaseDirectory 'SHA256SUMS.txt')) {
        if ($line -notmatch '^([0-9A-Fa-f]{64})  (.+)$') { "Invalid checksum line: $line"; continue }
        $file = Join-Path $releaseDirectory $Matches[2]
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { "Missing: $($Matches[2])"; continue }
        if ((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $Matches[1].ToUpperInvariant()) {
            "Mismatch: $($Matches[2])"
        }
    }
    foreach ($required in 'release-record.json', 'RELEASE_RECORD.md', "dx-platform-company-$Version.zip", "dx-platform-iis-$Version.zip") {
        if (-not (Test-Path -LiteralPath (Join-Path $releaseDirectory $required) -PathType Leaf)) {
            $mismatches = @($mismatches) + "Missing release file: $required"
        }
    }
    if ($mismatches) {
        throw ($mismatches -join [Environment]::NewLine)
    }

    Write-Host ''
    Write-Host "Release $Version is ready: $releaseDirectory" -ForegroundColor Green
    Write-Host '次の手順:'
    Write-Host '  1. RELEASE_RECORD.md をGitHubのリリースIssueとSharePointの承認記録へ転記する（ZIPのSHA-256を含む）。'
    Write-Host '  2. 承認後、許可されたUSBとウイルスチェック手順で社内へ持ち込む。'
    Write-Host '  3. サーバーでは、承認記録のSHA-256とZIPを照合してから配置する。'
    Get-Content -LiteralPath (Join-Path $releaseDirectory 'RELEASE_RECORD.md')
}
catch {
    Write-Host "RELEASE FAILED: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    if (Test-Path -LiteralPath $extractPath) {
        Remove-Item -LiteralPath $extractPath -Recurse -Force
    }
    Pop-Location
}
