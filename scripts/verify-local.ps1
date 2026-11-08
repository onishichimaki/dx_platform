<#
.SYNOPSIS
  開発PCでリリース前検証を行い、検証証跡（verification.json）を出力する。

.DESCRIPTION
  GitHub Actionsを使わずに、次の検証をすべて実行する。1つでも失敗した場合は終了コード1を返す。
    1. 作業ツリーがclean（未commit変更なし）であること
    2. NuGet監査付きの強制restore（NuGetAuditMode=all。NU1900-NU1904は失敗扱い）
    3. 脆弱な依存（推移的依存を含む）が0件であること
    4. Release構成のbuild
    5. tests配下の全テストプロジェクトのtest（0件実行・失敗・TRX欠落は失敗）
    6. EF Core Migrationとモデルの差分がないこと
    7. 検証中に作業ツリーが変更されていないこと
  証跡は artifacts/verify/<commit8>-<UTC時刻>/ に出力する（Git管理対象外）。
  -AllowDirtyWorkingTree を指定した場合、証跡は releasable=false となり、リリースには使えない。
#>
#Requires -Version 7.0
[CmdletBinding()]
param(
    [string]$DotNetPath,

    [string]$OutputDirectory = 'artifacts/verify',

    [switch]$AllowDirtyWorkingTree
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

. (Join-Path $PSScriptRoot 'lib/ReleaseCommon.ps1')
$script:DotNet = Resolve-DotNetPath -Requested $DotNetPath -RepositoryRoot $repositoryRoot

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

$startedAt = [DateTimeOffset]::UtcNow
$script:steps = [System.Collections.Generic.List[object]]::new()
$script:failedStep = $null
$script:failureMessage = $null
$script:sdkVersion = $null
$script:workingTreeClean = $false
$script:vulnerablePackages = [System.Collections.Generic.List[object]]::new()
$script:auditProblems = [System.Collections.Generic.List[string]]::new()
$script:testProjects = [System.Collections.Generic.List[object]]::new()
$script:pendingModelChanges = $null
$commit = $null
$branch = $null
$tags = @()
$evidenceDirectory = $null
$logDirectory = $null
$trxDirectory = $null

function Write-Utf8File {
    param([string]$Path, [string[]]$Lines)
    [System.IO.File]::WriteAllLines($Path, $Lines, $utf8NoBom)
}

function Invoke-Native {
    param([string]$Name, [string[]]$Arguments)
    $errorPath = Join-Path $logDirectory "$Name.err.log"
    $lines = @(& $script:DotNet @Arguments 2>$errorPath | ForEach-Object { $_.ToString() })
    $code = $LASTEXITCODE
    Write-Utf8File (Join-Path $logDirectory "$Name.log") ([string[]]$lines)
    $errorLines = if (Test-Path -LiteralPath $errorPath) { @(Get-Content -LiteralPath $errorPath) } else { @() }
    [pscustomobject]@{ ExitCode = $code; Output = $lines; ErrorOutput = $errorLines }
}

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host "==> $Name"
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $status = 'Passed'
    $detail = $null
    try {
        & $Action
    }
    catch {
        $status = 'Failed'
        $detail = $_.Exception.Message
    }
    $stopwatch.Stop()
    $script:steps.Add([ordered]@{
            name = $Name
            status = $status
            durationSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
            detail = $detail
        })
    if ($status -eq 'Failed') {
        $script:failedStep = $Name
        $script:failureMessage = $detail
        Write-Host "    FAILED: $detail" -ForegroundColor Red
        throw $detail
    }
}

Push-Location $repositoryRoot
try {
    $commit = (git rev-parse HEAD).Trim()
    $branch = (git branch --show-current).Trim()
    $tags = @(git tag --points-at HEAD)
    $evidenceDirectory = Join-Path $resolvedOutput "$($commit.Substring(0, 8))-$($startedAt.ToString('yyyyMMddTHHmmssZ'))"
    $logDirectory = Join-Path $evidenceDirectory 'logs'
    $trxDirectory = Join-Path $evidenceDirectory 'trx'
    New-Item -ItemType Directory -Path $logDirectory, $trxDirectory -Force | Out-Null

    Invoke-Step 'WorkingTreeClean' {
        $changes = @(git status --porcelain)
        if ($changes.Count -gt 0) {
            if (-not $AllowDirtyWorkingTree) {
                throw "未commitの変更があります（$($changes.Count)件）。リリース対象のcommitに揃えてから実行してください。"
            }
            Write-Warning 'Working tree is dirty. The evidence will be marked releasable=false.'
        }
        else {
            $script:workingTreeClean = $true
        }
    }

    Invoke-Step 'DotNetSdk' {
        $result = Invoke-Native 'dotnet-version' @('--version')
        if ($result.ExitCode -ne 0 -or $result.Output.Count -eq 0) {
            throw 'dotnet SDK was not found or does not satisfy global.json.'
        }
        $script:sdkVersion = $result.Output[0].Trim()
    }

    Invoke-Step 'ScriptTests' {
        # リリース用スクリプト自身の判定ロジック（証跡の検証、改ざん検出など）の回帰テスト。
        $testScript = Join-Path $repositoryRoot 'tests/scripts/Invoke-ScriptTests.ps1'
        $lines = @(& pwsh -NoProfile -File $testScript 2>&1 | ForEach-Object { $_.ToString() })
        $code = $LASTEXITCODE
        Write-Utf8File (Join-Path $logDirectory 'script-tests.log') ([string[]]$lines)
        $summary = $lines | Where-Object { $_ -match '^(Script tests:|FAIL )' }
        if ($code -ne 0) {
            throw "Script tests failed. $($summary -join ' | ')"
        }
    }

    Invoke-Step 'RestoreWithNuGetAudit' {
        $result = Invoke-Native 'restore' @(
            'restore', 'DxPlatform.slnx', '--force',
            '-p:NuGetAudit=true', '-p:NuGetAuditMode=all')
        $combined = @($result.Output) + @($result.ErrorOutput)
        $auditLines = @($combined | Where-Object { $_ -match 'NU190[0-4]' })
        if ($result.ExitCode -ne 0 -or $auditLines.Count -gt 0) {
            throw "restore failed or NuGet audit reported NU1900-NU1904 (exit $($result.ExitCode), audit lines $($auditLines.Count))."
        }
    }

    Invoke-Step 'VulnerablePackages' {
        $result = Invoke-Native 'vulnerable-packages' @(
            'list', 'DxPlatform.slnx', 'package', '--vulnerable', '--include-transitive', '--format', 'json')
        $text = $result.Output -join "`n"
        if ($result.ExitCode -ne 0 -or $text.IndexOf('{') -lt 0) {
            throw 'dotnet list package --vulnerable could not produce a result (vulnerability data may be unreachable).'
        }
        $report = Get-VulnerablePackageReport -Json $text
        foreach ($problem in $report.Problems) { $script:auditProblems.Add($problem) }
        foreach ($package in $report.Packages) { $script:vulnerablePackages.Add($package) }
        if ($script:auditProblems.Count -gt 0) {
            throw "Vulnerability scan reported problems: $($script:auditProblems -join '; ')"
        }
        if ($script:vulnerablePackages.Count -gt 0) {
            $names = $script:vulnerablePackages | ForEach-Object { "$($_.id) $($_.resolvedVersion)" } | Sort-Object -Unique
            throw "脆弱な依存が$($script:vulnerablePackages.Count)件あります: $($names -join ', ')"
        }
    }

    Invoke-Step 'BuildRelease' {
        $result = Invoke-Native 'build' @('build', 'DxPlatform.slnx', '-c', 'Release', '--no-restore', '-p:ContinuousIntegrationBuild=true')
        if ($result.ExitCode -ne 0) {
            throw "build failed (exit $($result.ExitCode)). See logs/build.log."
        }
    }

    Invoke-Step 'Tests' {
        $projects = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'tests') -Filter '*.csproj' -Recurse | Sort-Object FullName)
        if ($projects.Count -eq 0) {
            throw 'No test project was found under tests/.'
        }
        $failures = [System.Collections.Generic.List[string]]::new()
        foreach ($project in $projects) {
            $name = $project.BaseName
            $result = Invoke-Native "test-$name" @(
                'test', $project.FullName, '-c', 'Release', '--no-build',
                '--logger', "trx;LogFileName=$name.trx", '--results-directory', $trxDirectory)
            $trxPath = Join-Path $trxDirectory "$name.trx"
            if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
                $failures.Add("${name}: TRXが出力されませんでした（exit $($result.ExitCode)）")
                continue
            }
            $summary = Get-TrxSummary $trxPath
            $script:testProjects.Add([ordered]@{
                    name = $name
                    total = $summary.total
                    executed = $summary.executed
                    passed = $summary.passed
                    failed = $summary.failed
                    skipped = $summary.skipped
                    trxSha256 = (Get-FileHash -LiteralPath $trxPath -Algorithm SHA256).Hash
                })
            if ($result.ExitCode -ne 0 -or $summary.failed -gt 0) {
                $failures.Add("${name}: 失敗$($summary.failed)件（exit $($result.ExitCode)）")
            }
            elseif ($summary.executed -eq 0) {
                $failures.Add("${name}: 実行されたテストが0件です")
            }
        }
        if ($failures.Count -gt 0) {
            throw ($failures -join '; ')
        }
    }

    Invoke-Step 'MigrationModelDiff' {
        $restoreTool = Invoke-Native 'tool-restore' @('tool', 'restore')
        if ($restoreTool.ExitCode -ne 0) {
            throw 'dotnet tool restore failed.'
        }
        $result = Invoke-Native 'migrations-diff' @(
            'ef', 'migrations', 'has-pending-model-changes',
            '--project', 'src/DxPlatform.Infrastructure',
            '--startup-project', 'src/DxPlatform.Web',
            '--configuration', 'Release', '--no-build')
        if ($result.ExitCode -ne 0) {
            $script:pendingModelChanges = $true
            throw 'モデルとMigrationに差分があります。Migrationを追加するか、モデル変更を戻してください。'
        }
        $script:pendingModelChanges = $false
    }

    Invoke-Step 'WorkingTreeUnchanged' {
        $changes = @(git status --porcelain)
        if ($script:workingTreeClean -and $changes.Count -gt 0) {
            throw '検証中に追跡対象のファイルが変更されました。'
        }
        if ((git rev-parse HEAD).Trim() -ne $commit) {
            throw '検証中にHEADが変わりました。'
        }
    }
}
catch {
    if ($null -eq $script:failedStep) {
        $script:failedStep = 'Unexpected'
        $script:failureMessage = $_.Exception.Message
        Write-Host "FAILED: $($script:failureMessage)" -ForegroundColor Red
    }
}
finally {
    Pop-Location
}

$passed = ($null -eq $script:failedStep)
$testTotals = [ordered]@{ total = 0; executed = 0; passed = 0; failed = 0; skipped = 0 }
foreach ($project in $script:testProjects) {
    foreach ($key in @($testTotals.Keys)) { $testTotals[$key] += $project[$key] }
}
$tests = [ordered]@{
    total = $testTotals.total
    executed = $testTotals.executed
    passed = $testTotals.passed
    failed = $testTotals.failed
    skipped = $testTotals.skipped
    projects = @($script:testProjects)
}

if ($null -ne $evidenceDirectory) {
    $evidence = [ordered]@{
        schemaVersion = 1
        sourceCommit = $commit
        sourceBranch = $branch
        sourceTags = @($tags)
        workingTreeClean = $script:workingTreeClean
        releasable = ($passed -and $script:workingTreeClean)
        passed = $passed
        failedStep = $script:failedStep
        failureMessage = $script:failureMessage
        startedAtUtc = $startedAt.ToString('O')
        finishedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        dotnetSdkVersion = $script:sdkVersion
        nugetAudit = [ordered]@{
            mode = 'all'
            vulnerablePackageCount = $script:vulnerablePackages.Count
            vulnerablePackages = @($script:vulnerablePackages)
            problems = @($script:auditProblems)
        }
        tests = $tests
        migrationCheck = [ordered]@{ pendingModelChanges = $script:pendingModelChanges }
        steps = @($script:steps)
    }
    $evidence | ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath (Join-Path $evidenceDirectory 'verification.json') -Encoding utf8NoBOM
    $tests | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $evidenceDirectory 'test-summary.json') -Encoding utf8NoBOM

    Write-Host ''
    Write-Host "Evidence: $evidenceDirectory"
    Write-Host ("Result  : {0}  (tests {1} passed / {2} failed / {3} skipped, vulnerable packages {4})" -f
        $(if ($passed) { 'PASSED' } else { "FAILED at $($script:failedStep)" }),
        $tests.passed, $tests.failed, $tests.skipped, $script:vulnerablePackages.Count)
    if ($passed -and -not $script:workingTreeClean) {
        Write-Warning 'Dirty working tree: this evidence is NOT releasable.'
    }
}

if (-not $passed) {
    exit 1
}
