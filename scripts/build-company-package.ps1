[CmdletBinding()]
param(
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]{0,63}$')]
    [string]$Version = '0.1.0-rc1',

    [string]$OutputDirectory = 'artifacts/company',

    # 省略時は、リポジトリ直下の.dotnet\dotnet.exe（固定SDK）があればそれを、なければdotnetを使う。
    [string]$DotNetPath,

    [switch]$NoRestore,

    # 検証証跡（verify-local.ps1の出力ディレクトリまたはverification.json）。省略時はartifacts/verify内で
    # 現在のcommitに一致する最新の証跡を使う。
    [string]$VerificationPath,

    # HEADにv<Version>のtagが付いていることを必須にする（release-local.ps1が指定する）。
    [switch]$RequireTag,

    # ローカル試作用。未commit変更を許可し、検証証跡を要求しない。出力はreleasable=falseになる。
    [switch]$AllowDirtyWorkingTree
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    $localDotNet = Join-Path $repositoryRoot '.dotnet/dotnet.exe'
    $DotNetPath = if (Test-Path -LiteralPath $localDotNet -PathType Leaf) { $localDotNet } else { 'dotnet' }
}
$resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}
$repositoryPrefix = $repositoryRoot.TrimEnd('\') + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedOutput.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be a child of the repository root.'
}

$releaseDirectory = Join-Path $resolvedOutput 'release'
$workingDirectory = Join-Path $resolvedOutput "work-$Version"
$packageRoot = Join-Path $workingDirectory "dx-platform-company-$Version"
function Invoke-DotNet {
    param([Parameter(Mandatory)][string[]]$Arguments)
    & $DotNetPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed: $($Arguments -join ' ')"
    }
}

Push-Location $repositoryRoot
try {
    if (-not $AllowDirtyWorkingTree -and (git status --porcelain)) {
        throw 'Working tree is dirty. Commit the exact release source before packaging, or use -AllowDirtyWorkingTree only for a local trial build.'
    }

    $sourceCommit = (git rev-parse HEAD).Trim()
    $sourceTags = @(git tag --points-at HEAD)
    if ($RequireTag -and ($sourceTags -notcontains "v$Version")) {
        throw "HEAD does not have the release tag v$Version. Create the tag on the verified commit first."
    }

    # --- 検証証跡（必須）: テスト、NuGet脆弱性監査、Migration差分が現在のcommitで成功していること ---
    $verification = $null
    $verificationDirectory = $null
    if (-not $AllowDirtyWorkingTree) {
        $evidenceRoot = Join-Path $repositoryRoot 'artifacts/verify'
        if ($VerificationPath) {
            $given = [System.IO.Path]::GetFullPath($VerificationPath)
            $verificationDirectory = if (Test-Path -LiteralPath $given -PathType Leaf) { Split-Path -Parent $given } else { $given }
        }
        else {
            $prefix = $sourceCommit.Substring(0, 8)
            $latest = Get-ChildItem -LiteralPath $evidenceRoot -Directory -ErrorAction SilentlyContinue |
                Where-Object Name -Like "$prefix-*" |
                Sort-Object Name -Descending |
                Select-Object -First 1
            if ($null -eq $latest) {
                throw "No verification evidence for commit $prefix. Run scripts/verify-local.ps1 first."
            }
            $verificationDirectory = $latest.FullName
        }
        $verificationFile = Join-Path $verificationDirectory 'verification.json'
        $testSummaryFile = Join-Path $verificationDirectory 'test-summary.json'
        foreach ($required in @($verificationFile, $testSummaryFile)) {
            if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
                throw "Verification evidence is incomplete: $required"
            }
        }
        $verification = Get-Content -LiteralPath $verificationFile -Raw | ConvertFrom-Json
        $problems = [System.Collections.Generic.List[string]]::new()
        if ($verification.schemaVersion -ne 1) { $problems.Add('schemaVersion is not 1') }
        if ($verification.sourceCommit -ne $sourceCommit) { $problems.Add("evidence commit $($verification.sourceCommit) differs from HEAD $sourceCommit") }
        if ($verification.passed -ne $true) { $problems.Add("verification did not pass (failed step: $($verification.failedStep))") }
        if ($verification.releasable -ne $true -or $verification.workingTreeClean -ne $true) { $problems.Add('evidence is not releasable (dirty working tree)') }
        if ($verification.tests.executed -le 0) { $problems.Add('no tests were executed') }
        if ($verification.tests.failed -ne 0) { $problems.Add("$($verification.tests.failed) test(s) failed") }
        if ($verification.nugetAudit.vulnerablePackageCount -ne 0) { $problems.Add('vulnerable packages were reported') }
        if ($verification.migrationCheck.pendingModelChanges -ne $false) { $problems.Add('migration/model diff check did not confirm "no changes"') }
        if ($problems.Count -gt 0) {
            throw "Verification evidence rejected: $($problems -join '; '). Re-run scripts/verify-local.ps1."
        }
        $verificationSha256 = (Get-FileHash -LiteralPath $verificationFile -Algorithm SHA256).Hash
    }

    # 事前検証（clean tree、tag、検証証跡）を通過してから出力先を初期化する。
    # 検証に失敗した試行が、前回作成済みのリリース成果物を消さないようにするため。
    if (Test-Path -LiteralPath $workingDirectory) {
        Remove-Item -LiteralPath $workingDirectory -Recurse -Force
    }
    if (Test-Path -LiteralPath $releaseDirectory) {
        Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Path $packageRoot, $releaseDirectory -Force | Out-Null

    if (-not $NoRestore) {
        Invoke-DotNet @('restore', 'DxPlatform.slnx')
    }
    Invoke-DotNet @(
        'publish', 'src/DxPlatform.Web/DxPlatform.Web.csproj',
        '-c', 'Release', '--no-restore', '-o', (Join-Path $packageRoot 'app'))
    Invoke-DotNet @('tool', 'restore')
    New-Item -ItemType Directory -Path (Join-Path $packageRoot 'database') -Force | Out-Null
    Invoke-DotNet @(
        'ef', 'migrations', 'script', '--idempotent',
        '--project', 'src/DxPlatform.Infrastructure',
        '--startup-project', 'src/DxPlatform.Web',
        '--configuration', 'Release', '--no-build',
        '--output', (Join-Path $packageRoot 'database/dxplatform-migrations.sql'))

    $dependencyOutput = & $DotNetPath list DxPlatform.slnx package --include-transitive --format json
    if ($LASTEXITCODE -ne 0) {
        throw 'Dependency inventory generation failed.'
    }
    New-Item -ItemType Directory -Path (Join-Path $packageRoot 'inventory') -Force | Out-Null
    [System.IO.File]::WriteAllLines(
        (Join-Path $packageRoot 'inventory/dotnet-dependencies.json'),
        [string[]]$dependencyOutput,
        [System.Text.UTF8Encoding]::new($false))

    New-Item -ItemType Directory -Path (Join-Path $packageRoot 'config') -Force | Out-Null
    Copy-Item 'src/DxPlatform.Web/appsettings.Example.json' `
        (Join-Path $packageRoot 'config/appsettings.Production.template.json')

    $packageScripts = @(
        'deploy-iis.ps1',
        'rollback-iis.ps1',
        'smoke-test.ps1',
        'test-company-package.ps1'
    )
    New-Item -ItemType Directory -Path (Join-Path $packageRoot 'scripts') -Force | Out-Null
    foreach ($script in $packageScripts) {
        Copy-Item (Join-Path 'scripts' $script) (Join-Path $packageRoot 'scripts')
    }

    $packageDocuments = @(
        'COMPANY_SETUP_GUIDE.md',
        'DOWNLOAD_PACKAGE.md',
        'HANDOFF_TO_COMPANY.md',
        'COMPANY_ENVIRONMENT_INPUTS.md',
        'COMPANY_COMPATIBILITY_CHECKLIST.md',
        'SECURITY.md',
        'DEPLOYMENT.md',
        'OPERATIONS.md',
        'KNOWN_LIMITATIONS.md',
        'OSS_LICENSE_REVIEW.md'
    )
    New-Item -ItemType Directory -Path (Join-Path $packageRoot 'docs') -Force | Out-Null
    foreach ($document in $packageDocuments) {
        Copy-Item (Join-Path 'docs' $document) (Join-Path $packageRoot 'docs')
    }

    # 検証証跡を同梱する。生のTRXにはPC名・ユーザー名が含まれるため同梱せず、件数とSHA-256のみを記録する。
    if ($null -ne $verification) {
        New-Item -ItemType Directory -Path (Join-Path $packageRoot 'evidence') -Force | Out-Null
        Copy-Item $verificationFile (Join-Path $packageRoot 'evidence/verification.json')
        Copy-Item $testSummaryFile (Join-Path $packageRoot 'evidence/test-summary.json')
    }

    Remove-Item (Join-Path $packageRoot 'app/appsettings.Development.json') -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $packageRoot 'app/appsettings.Example.json') -Force -ErrorAction SilentlyContinue
    Get-ChildItem (Join-Path $packageRoot 'app') -Filter '*.map' -File -Recurse |
        Remove-Item -Force

    $commit = (git rev-parse HEAD).Trim()
    $migrations = Get-ChildItem 'src/DxPlatform.Infrastructure/Persistence/Migrations' -Filter '*.cs' |
        Where-Object { $_.Name -Match '^\d{14}_.+\.cs$' -and $_.Name -NotLike '*.Designer.cs' } |
        ForEach-Object BaseName
    $verificationSummary = $null
    if ($null -ne $verification) {
        $verificationSummary = [ordered]@{
            evidenceSha256 = $verificationSha256
            verifiedAtUtc = $verification.finishedAtUtc
            dotnetSdkVersion = $verification.dotnetSdkVersion
            tests = [ordered]@{
                total = $verification.tests.total
                passed = $verification.tests.passed
                failed = $verification.tests.failed
                skipped = $verification.tests.skipped
            }
            nugetAudit = [ordered]@{
                mode = $verification.nugetAudit.mode
                vulnerablePackageCount = $verification.nugetAudit.vulnerablePackageCount
            }
            migrationCheck = [ordered]@{ pendingModelChanges = $verification.migrationCheck.pendingModelChanges }
        }
    }
    $manifest = [ordered]@{
        packageVersion = $Version
        sourceCommit = $commit
        sourceTags = @($sourceTags)
        releasable = ($null -ne $verification)
        target = 'Windows Server / IIS / SQL Server'
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        containsSecrets = $false
        databaseApplyMigrationsOnStartup = $false
        migrations = @($migrations)
        verification = $verificationSummary
    }
    $manifest | ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath (Join-Path $packageRoot 'manifest.json') -Encoding utf8NoBOM

    $forbiddenExtensions = @('.pfx', '.p12', '.pem', '.key', '.db', '.sqlite', '.sqlite3')
    $forbiddenFiles = Get-ChildItem -LiteralPath $packageRoot -File -Recurse |
        Where-Object { $forbiddenExtensions -contains $_.Extension.ToLowerInvariant() -or $_.Name -eq '.env' }
    if ($forbiddenFiles) {
        throw "Forbidden file found in package: $($forbiddenFiles.FullName -join ', ')"
    }
    $localMachineMarker = Select-String -Path (Get-ChildItem $packageRoot -File -Recurse).FullName `
        -Pattern 'DESKTOP-RQ3T767' -SimpleMatch -ErrorAction SilentlyContinue
    if ($localMachineMarker) {
        throw 'A local machine identifier was found in the package.'
    }

    $checksumLines = Get-ChildItem -LiteralPath $packageRoot -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($packageRoot, $_.FullName).Replace('\', '/')
            "$(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 | Select-Object -ExpandProperty Hash)  $relative"
        }
    [System.IO.File]::WriteAllLines(
        (Join-Path $packageRoot 'SHA256SUMS.txt'),
        [string[]]$checksumLines,
        [System.Text.UTF8Encoding]::new($false))

    $applicationZip = Join-Path $releaseDirectory "dx-platform-iis-$Version.zip"
    $companyZip = Join-Path $releaseDirectory "dx-platform-company-$Version.zip"
    Compress-Archive -Path (Join-Path $packageRoot 'app/*') -DestinationPath $applicationZip -Force
    Compress-Archive -Path (Join-Path $packageRoot '*') -DestinationPath $companyZip -Force
    Copy-Item (Join-Path $packageRoot 'database/dxplatform-migrations.sql') `
        (Join-Path $releaseDirectory "dxplatform-migrations-$Version.sql")

    # リリース記録: GitHubのリリースIssueとSharePointの承認記録へ転記する値。
    # サーバー側では承認記録のZIPのSHA-256と照合する（USB内のSHA256SUMS.txtは破損検知用）。
    $recordArtifacts = Get-ChildItem -LiteralPath $releaseDirectory -File | Sort-Object Name | ForEach-Object {
        [ordered]@{
            name = $_.Name
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
    $releaseRecord = [ordered]@{
        schemaVersion = 1
        packageVersion = $Version
        sourceCommit = $commit
        sourceTags = @($sourceTags)
        releasable = ($null -ne $verification)
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        verification = $verificationSummary
        artifacts = @($recordArtifacts)
    }
    $releaseRecord | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $releaseDirectory 'release-record.json') -Encoding utf8NoBOM
    $recordLines = [System.Collections.Generic.List[string]]::new()
    $recordLines.Add("# リリース記録 $Version")
    $recordLines.Add('')
    $recordLines.Add("- バージョン: $Version")
    $recordLines.Add("- ソースコミット: $commit")
    $recordLines.Add("- タグ: $(if ($sourceTags.Count -gt 0) { $sourceTags -join ', ' } else { '(なし)' })")
    $recordLines.Add("- リリース可否: $(if ($null -ne $verification) { 'releasable' } else { '試作（検証証跡なし。リリース不可）' })")
    if ($null -ne $verificationSummary) {
        $recordLines.Add("- テスト: $($verificationSummary.tests.passed)件成功 / $($verificationSummary.tests.failed)件失敗 / $($verificationSummary.tests.skipped)件スキップ（計$($verificationSummary.tests.total)件）")
        $recordLines.Add("- NuGet脆弱性監査（mode=$($verificationSummary.nugetAudit.mode)）: 脆弱な依存 $($verificationSummary.nugetAudit.vulnerablePackageCount)件")
        $recordLines.Add("- Migration差分: $(if ($verificationSummary.migrationCheck.pendingModelChanges -eq $false) { 'なし' } else { '要確認' })")
        $recordLines.Add("- .NET SDK: $($verificationSummary.dotnetSdkVersion)")
        $recordLines.Add("- 検証証跡(verification.json)のSHA-256: $($verificationSummary.evidenceSha256)")
    }
    $recordLines.Add('')
    $recordLines.Add('| ファイル | バイト数 | SHA-256 |')
    $recordLines.Add('|---|---|---|')
    foreach ($artifact in $recordArtifacts) {
        $recordLines.Add("| $($artifact.name) | $($artifact.bytes) | $($artifact.sha256) |")
    }
    [System.IO.File]::WriteAllLines(
        (Join-Path $releaseDirectory 'RELEASE_RECORD.md'),
        [string[]]$recordLines,
        [System.Text.UTF8Encoding]::new($false))

    $releaseChecksums = Get-ChildItem -LiteralPath $releaseDirectory -File |
        Sort-Object Name |
        ForEach-Object {
            "$(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256 | Select-Object -ExpandProperty Hash)  $($_.Name)"
        }
    [System.IO.File]::WriteAllLines(
        (Join-Path $releaseDirectory 'SHA256SUMS.txt'),
        [string[]]$releaseChecksums,
        [System.Text.UTF8Encoding]::new($false))

    Write-Host "Company release package created: $releaseDirectory"
    Get-ChildItem -LiteralPath $releaseDirectory -File | Select-Object Name, Length
}
finally {
    Pop-Location
}
