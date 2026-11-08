<#
.SYNOPSIS
  リリース用PowerShellスクリプトの回帰テスト（外部モジュール不要）。

.DESCRIPTION
  verify-local.ps1 の1ステップとして必ず実行される（単独実行も可: pwsh -NoProfile -File tests/scripts/Invoke-ScriptTests.ps1）。
  次の「再現したい異常系」を固定する。dotnet、ネットワーク、実リポジトリの作業ツリーは使わない。
    - 検証証跡の判定（commit不一致、失敗、0件実行、脆弱性、Migration差分、古い証跡）
    - TRX集計と脆弱性レポート解析
    - package検証（改ざん、証跡の差替え・削除、未登録ファイル、試作package）
    - 事前条件の失敗（clean tree、tag、証跡なし）と、失敗時に前回の成果物を消さないこと
  失敗が1件でもあれば終了コード1。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repositoryRoot 'scripts/lib/ReleaseCommon.ps1')

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "dxplatform-script-tests-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$script:results = [System.Collections.Generic.List[object]]::new()
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Test-Case {
    param([string]$Name, [scriptblock]$Body)
    try {
        & $Body
        $script:results.Add([pscustomobject]@{ Name = $Name; Passed = $true; Message = $null })
    }
    catch {
        $script:results.Add([pscustomobject]@{ Name = $Name; Passed = $false; Message = $_.Exception.Message })
    }
}

function Assert-True {
    param($Condition, [string]$Message)
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function Assert-Equal {
    param($Actual, $Expected, [string]$Message)
    if ($Actual -ne $Expected) { throw "Assertion failed: $Message (expected '$Expected', actual '$Actual')" }
}

function Assert-Match {
    param([string]$Text, [string]$Pattern, [string]$Message)
    if ($Text -notmatch $Pattern) { throw "Assertion failed: $Message (pattern '$Pattern' not found)" }
}

function Invoke-ChildScript {
    param([string]$Script, [string[]]$Arguments = @(), [string]$WorkingDirectory = $tempRoot)
    Push-Location $WorkingDirectory
    try {
        $output = & pwsh -NoProfile -File $Script @Arguments 2>&1 | Out-String
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
    }
    finally { Pop-Location }
}

function Write-Json {
    param([string]$Path, $Value)
    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, ($Value | ConvertTo-Json -Depth 8), $utf8NoBom)
}

function New-Evidence {
    param([string]$Commit = ('a' * 40), [DateTimeOffset]$FinishedAt = [DateTimeOffset]::UtcNow)
    [pscustomobject]@{
        schemaVersion = 1
        sourceCommit = $Commit
        passed = $true
        failedStep = $null
        workingTreeClean = $true
        releasable = $true
        finishedAtUtc = $FinishedAt.ToString('O')
        tests = [pscustomobject]@{ total = 3; executed = 3; passed = 3; failed = 0; skipped = 0 }
        nugetAudit = [pscustomobject]@{ mode = 'all'; vulnerablePackageCount = 0 }
        migrationCheck = [pscustomobject]@{ pendingModelChanges = $false }
        steps = @($script:RequiredVerificationSteps | ForEach-Object { [pscustomobject]@{ name = $_; status = 'Passed' } })
    }
}

function Copy-Object {
    param($Value)
    $Value | ConvertTo-Json -Depth 8 | ConvertFrom-Json
}

# --- package fixture（test-company-package.ps1 が要求する最小構成） ---
function Update-Checksums {
    param([string]$Root)
    $sums = Join-Path $Root 'SHA256SUMS.txt'
    $lines = Get-ChildItem -LiteralPath $Root -File -Recurse |
        Where-Object FullName -ne $sums |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [System.IO.Path]::GetRelativePath($Root, $_.FullName).Replace('\', '/')
            "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $relative"
        }
    [System.IO.File]::WriteAllLines($sums, [string[]]$lines, $utf8NoBom)
}

function New-FixturePackage {
    param([string]$Root, [bool]$Releasable = $true)
    foreach ($directory in 'app', 'database', 'config', 'docs') {
        New-Item -ItemType Directory -Path (Join-Path $Root $directory) -Force | Out-Null
    }
    Set-Content (Join-Path $Root 'app/DxPlatform.Web.dll') 'dll' -Encoding utf8NoBOM
    Set-Content (Join-Path $Root 'app/web.config') '<configuration />' -Encoding utf8NoBOM
    Set-Content (Join-Path $Root 'database/dxplatform-migrations.sql') 'select 1;' -Encoding utf8NoBOM
    Set-Content (Join-Path $Root 'config/appsettings.Production.template.json') '{}' -Encoding utf8NoBOM
    Set-Content (Join-Path $Root 'docs/COMPANY_SETUP_GUIDE.md') 'guide' -Encoding utf8NoBOM
    $commit = 'b' * 40
    $verificationSummary = $null
    if ($Releasable) {
        $evidence = New-Evidence -Commit $commit
        Write-Json (Join-Path $Root 'evidence/verification.json') $evidence
        Write-Json (Join-Path $Root 'evidence/test-summary.json') $evidence.tests
        $verificationSummary = [pscustomobject]@{
            evidenceSha256 = (Get-FileHash (Join-Path $Root 'evidence/verification.json') -Algorithm SHA256).Hash
        }
    }
    Write-Json (Join-Path $Root 'manifest.json') ([pscustomobject]@{
            packageVersion = '1.0.0'
            sourceCommit = $commit
            releasable = $Releasable
            containsSecrets = $false
            databaseApplyMigrationsOnStartup = $false
            verification = $verificationSummary
        })
    Update-Checksums $Root
}

function Test-Package {
    param([string]$Root)
    Invoke-ChildScript (Join-Path $repositoryRoot 'scripts/test-company-package.ps1') @('-PackageRoot', $Root)
}

# --- 一時Gitリポジトリ（scripts/ のコピー）で事前条件の失敗を再現する ---
function New-TempRepository {
    param([string]$Name)
    $root = Join-Path $tempRoot $Name
    New-Item -ItemType Directory -Path $root | Out-Null
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts') -Destination (Join-Path $root 'scripts') -Recurse
    Set-Content (Join-Path $root '.gitignore') "artifacts/`n" -Encoding utf8NoBOM
    Push-Location $root
    try {
        Invoke-TestGit init -q
        Invoke-TestGit add -A
        Invoke-TestGit commit -q -m init
        $commit = (git rev-parse HEAD).Trim()
        if ($commit -notmatch '^[0-9a-f]{40}$') { throw 'The temporary repository could not be created.' }
    }
    finally { Pop-Location }
    [pscustomobject]@{ Root = $root; Commit = $commit }
}

function Invoke-TestGit {
    # 利用者のglobal設定（commit.gpgsign、hooks、autocrlfなど）に依存しないようにする。
    $isolated = @('-c', 'user.name=tester', '-c', 'user.email=tester@example.invalid',
        '-c', 'commit.gpgsign=false', '-c', 'tag.gpgsign=false', '-c', 'core.hooksPath=NUL', '-c', 'core.autocrlf=false')
    $previous = $env:GIT_CONFIG_NOSYSTEM
    $env:GIT_CONFIG_NOSYSTEM = '1'
    try {
        $output = & git @isolated @args 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed: $output" }
    }
    finally { $env:GIT_CONFIG_NOSYSTEM = $previous }
}

try {
    # ===== 検証証跡の判定 =====
    $commit = 'a' * 40
    Test-Case 'evidence: valid evidence is accepted' {
        $problems = Test-VerificationEvidence -Evidence (New-Evidence) -SourceCommit $commit
        Assert-Equal $problems.Count 0 'no problems expected'
    }
    $evidenceCases = @(
        @{ Name = 'commit mismatch'; Expect = 'differs from HEAD'; Edit = { param($e) $e.sourceCommit = ('0' * 40) } }
        @{ Name = 'verification failed'; Expect = 'did not pass'; Edit = { param($e) $e.passed = $false; $e.failedStep = 'Tests' } }
        @{ Name = 'not releasable'; Expect = 'not releasable'; Edit = { param($e) $e.releasable = $false } }
        @{ Name = 'dirty working tree'; Expect = 'not releasable'; Edit = { param($e) $e.workingTreeClean = $false } }
        @{ Name = 'zero tests executed'; Expect = 'no tests were executed'; Edit = { param($e) $e.tests.executed = 0 } }
        @{ Name = 'a test failed'; Expect = 'failed'; Edit = { param($e) $e.tests.failed = 1 } }
        @{ Name = 'vulnerable package'; Expect = 'vulnerable packages'; Edit = { param($e) $e.nugetAudit.vulnerablePackageCount = 1 } }
        @{ Name = 'migration diff unconfirmed'; Expect = 'migration/model diff'; Edit = { param($e) $e.migrationCheck.pendingModelChanges = $null } }
        @{ Name = 'migration diff present'; Expect = 'migration/model diff'; Edit = { param($e) $e.migrationCheck.pendingModelChanges = $true } }
        @{ Name = 'unknown schema'; Expect = 'schemaVersion'; Edit = { param($e) $e.schemaVersion = 2 } }
        @{ Name = 'weaker audit mode'; Expect = 'audit mode'; Edit = { param($e) $e.nugetAudit.mode = 'direct' } }
        @{ Name = 'a required step is missing'; Expect = "required step 'MigrationModelDiff'"; Edit = { param($e) $e.steps = @($e.steps | Where-Object name -ne 'MigrationModelDiff') } }
        @{ Name = 'a required step did not pass'; Expect = "required step 'ScriptTests'"; Edit = { param($e) ($e.steps | Where-Object name -eq 'ScriptTests').status = 'Failed' } }
        @{ Name = 'no steps recorded'; Expect = 'required step'; Edit = { param($e) $e.steps = @() } }
        @{ Name = 'stale (3 days old)'; Expect = 'older than'; Edit = { param($e) $e.finishedAtUtc = [DateTimeOffset]::UtcNow.AddDays(-3).ToString('O') } }
        @{ Name = 'timestamp in the future'; Expect = 'future'; Edit = { param($e) $e.finishedAtUtc = [DateTimeOffset]::UtcNow.AddHours(3).ToString('O') } }
        @{ Name = 'timestamp missing'; Expect = 'finishedAtUtc'; Edit = { param($e) $e.finishedAtUtc = $null } }
    )
    foreach ($case in $evidenceCases) {
        Test-Case "evidence: rejects $($case.Name)" {
            $evidence = Copy-Object (New-Evidence)
            & $case.Edit $evidence
            $problems = Test-VerificationEvidence -Evidence $evidence -SourceCommit $commit
            Assert-True ($problems.Count -gt 0) 'a problem should be reported'
            Assert-Match ($problems -join ' | ') $case.Expect 'problem text'
        }
    }

    # ===== TRX集計 =====
    $trxNamespace = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'
    Test-Case 'trx: counts passed, failed (failed+error) and skipped' {
        $path = Join-Path $tempRoot 'sample.trx'
        Set-Content $path "<TestRun xmlns=`"$trxNamespace`"><ResultSummary outcome=`"Failed`"><Counters total=`"5`" executed=`"4`" passed=`"2`" failed=`"1`" error=`"1`" timeout=`"0`" aborted=`"0`" notExecuted=`"1`" /></ResultSummary></TestRun>" -Encoding utf8NoBOM
        $summary = Get-TrxSummary $path
        Assert-Equal $summary.total 5 'total'
        Assert-Equal $summary.executed 4 'executed'
        Assert-Equal $summary.passed 2 'passed'
        Assert-Equal $summary.failed 2 'failed'
        Assert-Equal $summary.skipped 1 'skipped'
    }
    Test-Case 'trx: a file without counters is an error' {
        $path = Join-Path $tempRoot 'broken.trx'
        Set-Content $path "<TestRun xmlns=`"$trxNamespace`"><ResultSummary /></TestRun>" -Encoding utf8NoBOM
        $threw = $false
        try { Get-TrxSummary $path | Out-Null } catch { $threw = $true }
        Assert-True $threw 'should throw'
    }

    # ===== 脆弱性レポート解析 =====
    Test-Case 'audit: a vulnerable transitive package is reported' {
        $json = '{"version":1,"projects":[{"path":"C:\\x\\Web.csproj","frameworks":[{"framework":"net10.0","transitivePackages":[{"id":"Foo.Bar","resolvedVersion":"1.0.0","vulnerabilities":[{"severity":"High","advisoryurl":"https://example.invalid/a"}]}]}]}]}'
        $report = Get-VulnerablePackageReport -Json $json
        Assert-Equal $report.Packages.Count 1 'package count'
        Assert-Equal $report.Packages[0].id 'Foo.Bar' 'package id'
        Assert-Equal $report.Packages[0].project 'Web' 'project name'
    }
    Test-Case 'audit: a clean report has no packages and no problems' {
        $json = '{"version":1,"projects":[{"path":"C:\\x\\Web.csproj","frameworks":[{"framework":"net10.0","topLevelPackages":[{"id":"Ok","resolvedVersion":"1.0.0"}]}]}]}'
        $report = Get-VulnerablePackageReport -Json $json
        Assert-Equal $report.Packages.Count 0 'package count'
        Assert-Equal $report.Problems.Count 0 'problem count'
    }
    Test-Case 'audit: problems such as unreachable vulnerability data are surfaced' {
        $json = 'warning text before json {"version":1,"problems":[{"level":"error","text":"unable to load the service index"}],"projects":[]}'
        $report = Get-VulnerablePackageReport -Json $json
        Assert-Equal $report.Problems.Count 1 'problem count'
        Assert-Match $report.Problems[0] 'unable to load' 'problem text'
    }
    Test-Case 'audit: output without JSON is an error' {
        $threw = $false
        try { Get-VulnerablePackageReport -Json 'no json here' | Out-Null } catch { $threw = $true }
        Assert-True $threw 'should throw'
    }

    # ===== package検証（test-company-package.ps1）=====
    Test-Case 'package: an untouched releasable package is accepted' {
        $root = Join-Path $tempRoot 'pkg-ok'; New-FixturePackage $root
        $result = Test-Package $root
        Assert-Equal $result.ExitCode 0 "exit code. $($result.Output)"
    }
    Test-Case 'package: a trial package is accepted with a warning by default' {
        $root = Join-Path $tempRoot 'pkg-trial'; New-FixturePackage $root -Releasable $false
        $result = Test-Package $root
        Assert-Equal $result.ExitCode 0 "exit code. $($result.Output)"
        Assert-Match $result.Output 'trial package' 'warning text'
    }
    Test-Case 'package: -RequireReleasable rejects a trial package' {
        $root = Join-Path $tempRoot 'pkg-trial-strict'; New-FixturePackage $root -Releasable $false
        $result = Invoke-ChildScript (Join-Path $repositoryRoot 'scripts/test-company-package.ps1') @('-PackageRoot', $root, '-RequireReleasable')
        Assert-True ($result.ExitCode -ne 0) 'must fail'
        Assert-Match $result.Output 'not a releasable package' 'message'
    }
    Test-Case 'package: -RequireReleasable rejects a manifest without the releasable flag' {
        $root = Join-Path $tempRoot 'pkg-noflag'; New-FixturePackage $root
        $manifestPath = Join-Path $root 'manifest.json'
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
        $manifest.PSObject.Properties.Remove('releasable')
        Write-Json $manifestPath $manifest; Update-Checksums $root
        $result = Invoke-ChildScript (Join-Path $repositoryRoot 'scripts/test-company-package.ps1') @('-PackageRoot', $root, '-RequireReleasable')
        Assert-True ($result.ExitCode -ne 0) 'must fail'
    }
    Test-Case 'package: -RequireReleasable accepts an untouched releasable package' {
        $root = Join-Path $tempRoot 'pkg-strict-ok'; New-FixturePackage $root
        $result = Invoke-ChildScript (Join-Path $repositoryRoot 'scripts/test-company-package.ps1') @('-PackageRoot', $root, '-RequireReleasable')
        Assert-Equal $result.ExitCode 0 "exit code. $($result.Output)"
    }
    Test-Case 'package: a file changed after checksums were written is rejected' {
        $root = Join-Path $tempRoot 'pkg-edit'; New-FixturePackage $root
        Add-Content (Join-Path $root 'app/web.config') 'tampered'
        Assert-True ((Test-Package $root).ExitCode -ne 0) 'must fail'
    }
    Test-Case 'package: evidence edited and checksums regenerated is rejected by the manifest binding' {
        $root = Join-Path $tempRoot 'pkg-rebind'; New-FixturePackage $root
        $path = Join-Path $root 'evidence/verification.json'
        $evidence = Get-Content $path -Raw | ConvertFrom-Json; $evidence.tests.executed = 999
        Write-Json $path $evidence; Update-Checksums $root
        $result = Test-Package $root
        Assert-True ($result.ExitCode -ne 0) 'must fail'
        Assert-Match $result.Output 'evidenceSha256' 'binding message'
    }
    Test-Case 'package: evidence that records a failure is rejected even with a matching manifest hash' {
        $root = Join-Path $tempRoot 'pkg-failed'; New-FixturePackage $root
        $path = Join-Path $root 'evidence/verification.json'
        $evidence = Get-Content $path -Raw | ConvertFrom-Json; $evidence.tests.failed = 1
        Write-Json $path $evidence
        $manifestPath = Join-Path $root 'manifest.json'
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
        $manifest.verification.evidenceSha256 = (Get-FileHash $path -Algorithm SHA256).Hash
        Write-Json $manifestPath $manifest; Update-Checksums $root
        Assert-True ((Test-Package $root).ExitCode -ne 0) 'must fail'
    }
    Test-Case 'package: a releasable package without its evidence file is rejected' {
        $root = Join-Path $tempRoot 'pkg-noevidence'; New-FixturePackage $root
        Remove-Item (Join-Path $root 'evidence/test-summary.json'); Update-Checksums $root
        Assert-True ((Test-Package $root).ExitCode -ne 0) 'must fail'
    }
    Test-Case 'package: a file that is not covered by the checksum list is rejected' {
        $root = Join-Path $tempRoot 'pkg-extra'; New-FixturePackage $root
        Set-Content (Join-Path $root 'app/extra.txt') 'x' -Encoding utf8NoBOM
        Assert-True ((Test-Package $root).ExitCode -ne 0) 'must fail'
    }
    Test-Case 'package: a manifest that claims secrets is rejected' {
        $root = Join-Path $tempRoot 'pkg-secrets'; New-FixturePackage $root
        $manifestPath = Join-Path $root 'manifest.json'
        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json; $manifest.containsSecrets = $true
        Write-Json $manifestPath $manifest; Update-Checksums $root
        Assert-True ((Test-Package $root).ExitCode -ne 0) 'must fail'
    }

    # ===== 事前条件の失敗（一時リポジトリ）=====
    $packageScript = 'scripts/build-company-package.ps1'
    Test-Case 'build: no verification evidence is rejected and the previous release output is kept' {
        $repo = New-TempRepository 'repo-noevidence'
        $keep = Join-Path $repo.Root 'artifacts/company/release/keep.txt'
        New-Item -ItemType Directory -Path (Split-Path -Parent $keep) -Force | Out-Null
        Set-Content $keep 'previous release' -Encoding utf8NoBOM
        $result = Invoke-ChildScript (Join-Path $repo.Root $packageScript) @('-Version', '1.0.0', '-NoRestore') $repo.Root
        Assert-True ($result.ExitCode -ne 0) 'must fail'
        Assert-Match $result.Output 'No verification evidence' 'message'
        Assert-True (Test-Path -LiteralPath $keep) 'previous release output must remain'
    }
    Test-Case 'build: -RequireTag without the release tag is rejected' {
        $repo = New-TempRepository 'repo-notag'
        $result = Invoke-ChildScript (Join-Path $repo.Root $packageScript) @('-Version', '1.0.0', '-NoRestore', '-RequireTag') $repo.Root
        Assert-True ($result.ExitCode -ne 0) 'must fail'
        Assert-Match $result.Output 'release tag v1\.0\.0' 'message'
    }
    Test-Case 'build: -RequireTag rejects a lightweight tag but accepts an annotated one' {
        $repo = New-TempRepository 'repo-tags'
        Push-Location $repo.Root
        try { Invoke-TestGit tag v1.0.0 } finally { Pop-Location }
        $result = Invoke-ChildScript (Join-Path $repo.Root $packageScript) @('-Version', '1.0.0', '-NoRestore', '-RequireTag') $repo.Root
        Assert-True ($result.ExitCode -ne 0) 'lightweight tag must fail'
        Assert-Match $result.Output 'annotated tag' 'message'
        Push-Location $repo.Root
        try { Invoke-TestGit tag -d v1.0.0; Invoke-TestGit tag -a v1.0.0 -m release } finally { Pop-Location }
        $result = Invoke-ChildScript (Join-Path $repo.Root $packageScript) @('-Version', '1.0.0', '-NoRestore', '-RequireTag') $repo.Root
        Assert-Match $result.Output 'No verification evidence' 'the tag check passes and the evidence check is next'
    }
    Test-Case 'build: a failure after the preconditions keeps the previous release output' {
        $repo = New-TempRepository 'repo-midfail'
        $directory = Join-Path $repo.Root "artifacts/verify/$($repo.Commit.Substring(0, 8))-20260101T000000Z"
        $evidence = New-Evidence -Commit $repo.Commit
        Write-Json (Join-Path $directory 'verification.json') $evidence
        Write-Json (Join-Path $directory 'test-summary.json') $evidence.tests
        $keep = Join-Path $repo.Root 'artifacts/company/release/keep.txt'
        New-Item -ItemType Directory -Path (Split-Path -Parent $keep) -Force | Out-Null
        Set-Content $keep 'previous release' -Encoding utf8NoBOM
        # この一時リポジトリにはsrc/が無いため、publishで必ず失敗する（事前条件は通過する）。
        $result = Invoke-ChildScript (Join-Path $repo.Root $packageScript) @('-Version', '1.0.0', '-NoRestore', '-MaxEvidenceAgeHours', '168') $repo.Root
        Assert-True ($result.ExitCode -ne 0) 'must fail at publish'
        Assert-True (-not ($result.Output -match 'No verification evidence|Verification evidence rejected')) "preconditions should pass. $($result.Output)"
        Assert-True (Test-Path -LiteralPath $keep) 'previous release output must remain'
    }
    Test-Case 'evidence lookup: a newer failed run does not hide an older passing run' {
        $root = Join-Path $tempRoot 'evidence-lookup'
        $good = New-Evidence -Commit $commit
        $bad = Copy-Object $good; $bad.passed = $false; $bad.failedStep = 'Tests'
        foreach ($case in @(@('aaaaaaaa-20260101T000000Z', $good), @('aaaaaaaa-20260102T000000Z', $bad))) {
            Write-Json (Join-Path $root "$($case[0])/verification.json") $case[1]
            Write-Json (Join-Path $root "$($case[0])/test-summary.json") $case[1].tests
        }
        $found = Find-VerificationEvidence -EvidenceRoot $root -SourceCommit $commit
        Assert-Match $found.Directory 'aaaaaaaa-20260101T000000Z' 'the passing run should be selected'
    }
    Test-Case 'evidence lookup: only failed runs raise the newest run problems' {
        $root = Join-Path $tempRoot 'evidence-lookup-bad'
        $bad = Copy-Object (New-Evidence -Commit $commit); $bad.passed = $false; $bad.failedStep = 'Tests'
        Write-Json (Join-Path $root 'aaaaaaaa-20260101T000000Z/verification.json') $bad
        Write-Json (Join-Path $root 'aaaaaaaa-20260101T000000Z/test-summary.json') $bad.tests
        $threw = $false
        try { Find-VerificationEvidence -EvidenceRoot $root -SourceCommit $commit | Out-Null } catch { $threw = $_.Exception.Message -match 'did not pass' }
        Assert-True $threw 'must throw with the reason'
    }
    Test-Case 'dotnet path: an explicit path wins, otherwise the repository SDK, otherwise dotnet' {
        $repo = Join-Path $tempRoot 'sdk-root'
        New-Item -ItemType Directory -Path $repo | Out-Null
        Assert-Equal (Resolve-DotNetPath -Requested 'custom.exe' -RepositoryRoot $repo) 'custom.exe' 'explicit'
        Assert-Equal (Resolve-DotNetPath -Requested '' -RepositoryRoot $repo) 'dotnet' 'fallback'
        New-Item -ItemType Directory -Path (Join-Path $repo '.dotnet') | Out-Null
        Set-Content (Join-Path $repo '.dotnet/dotnet.exe') 'x' -Encoding utf8NoBOM
        Assert-Match (Resolve-DotNetPath -Requested '' -RepositoryRoot $repo) '\.dotnet' 'repository SDK'
    }
    Test-Case 'build: a dirty working tree is rejected' {
        $repo = New-TempRepository 'repo-dirty'
        Set-Content (Join-Path $repo.Root 'uncommitted.txt') 'x' -Encoding utf8NoBOM
        $result = Invoke-ChildScript (Join-Path $repo.Root $packageScript) @('-Version', '1.0.0', '-NoRestore') $repo.Root
        Assert-True ($result.ExitCode -ne 0) 'must fail'
        Assert-Match $result.Output 'Working tree is dirty' 'message'
    }
    Test-Case 'build: stale evidence for the current commit is rejected' {
        $repo = New-TempRepository 'repo-stale'
        $directory = Join-Path $repo.Root "artifacts/verify/$($repo.Commit.Substring(0, 8))-20200101T000000Z"
        $evidence = New-Evidence -Commit $repo.Commit -FinishedAt ([DateTimeOffset]::UtcNow.AddDays(-3))
        Write-Json (Join-Path $directory 'verification.json') $evidence
        Write-Json (Join-Path $directory 'test-summary.json') $evidence.tests
        $result = Invoke-ChildScript (Join-Path $repo.Root $packageScript) @('-Version', '1.0.0', '-NoRestore') $repo.Root
        Assert-True ($result.ExitCode -ne 0) 'must fail'
        Assert-Match $result.Output 'older than' 'message'
    }
    Test-Case 'verify-local: a dirty working tree fails first and the evidence is not releasable' {
        $repo = New-TempRepository 'repo-verify-dirty'
        Set-Content (Join-Path $repo.Root 'uncommitted.txt') 'x' -Encoding utf8NoBOM
        $result = Invoke-ChildScript (Join-Path $repo.Root 'scripts/verify-local.ps1') @() $repo.Root
        Assert-Equal $result.ExitCode 1 'exit code'
        $evidenceFile = Get-ChildItem (Join-Path $repo.Root 'artifacts/verify') -Filter verification.json -Recurse | Select-Object -First 1
        Assert-True ($null -ne $evidenceFile) 'evidence must be written even on failure'
        $evidence = Get-Content $evidenceFile.FullName -Raw | ConvertFrom-Json
        Assert-Equal $evidence.failedStep 'WorkingTreeClean' 'failed step'
        Assert-Equal $evidence.passed $false 'passed'
        Assert-Equal $evidence.releasable $false 'releasable'
    }
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}

$failed = @($script:results | Where-Object { -not $_.Passed })
foreach ($result in $script:results) {
    if ($result.Passed) { Write-Host "ok   $($result.Name)" }
    else { Write-Host "FAIL $($result.Name): $($result.Message)" }
}
Write-Host ("Script tests: {0} passed, {1} failed" -f ($script:results.Count - $failed.Count), $failed.Count)
if ($failed.Count -gt 0) { exit 1 }
