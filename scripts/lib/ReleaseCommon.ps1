# verify-local.ps1 / build-company-package.ps1 が共有する関数。
# 判定ロジックをここに集め、tests/scripts/Invoke-ScriptTests.ps1 で単体テストする。
# 注意: package内で単体実行される test-company-package.ps1 はこのファイルに依存させない。

# verify-local.ps1 が必ず成功させるステップ。証跡にこれらがすべて Passed で含まれないとリリースに使えない。
$script:RequiredVerificationSteps = @(
    'WorkingTreeClean', 'DotNetSdk', 'ScriptTests', 'RestoreWithNuGetAudit', 'VulnerablePackages',
    'BuildRelease', 'Tests', 'MigrationModelDiff', 'WorkingTreeUnchanged')

function Get-LocalIdentifierMarkers {
    <#
      成果物に含めてはならない「この開発PC固有の識別子」の一覧を返す。特定のPC名を直書きしない。
        - 実行中のPC名、ユーザープロファイルのパス、リポジトリのパス（区切り文字の違いとJSONエスケープを含む）
        - 追加分: -Extra と環境変数 DX_RELEASE_FORBIDDEN_MARKERS（; 区切り）。社内固有の名称など、
          公開リポジトリに書けない値は、こちらで外部から与える
      4文字未満の値は誤検出が多いため無視する。
    #>
    param([Parameter(Mandatory)][string]$RepositoryRoot, [string[]]$Extra = @())

    $candidates = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @($env:COMPUTERNAME, $env:USERPROFILE, $RepositoryRoot)) {
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            $candidates.Add($value)
            $candidates.Add($value.Replace('\', '/'))
            $candidates.Add($value.Replace('\', '\\'))
        }
    }
    foreach ($value in @($Extra) + @($env:DX_RELEASE_FORBIDDEN_MARKERS -split ';')) {
        if (-not [string]::IsNullOrWhiteSpace($value)) { $candidates.Add($value.Trim()) }
    }
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($candidate in $candidates) {
        if ($candidate.Length -ge 4) { $null = $seen.Add($candidate) }
    }
    , @($seen)
}

function Find-IdentifierLeak {
    <#
      フォルダ内の全ファイルを検索し、いずれかのマーカーを含むファイルの相対パスを返す。
      テキストとしての出現に加え、.NETのDLL/PDBに埋め込まれるUTF-16の出現も検出する。
      戻り値はファイルの相対パスのみ（マーカーの値そのものは返さず、表示もしない）。
    #>
    param([Parameter(Mandatory)][string]$Root, [Parameter(Mandatory)][string[]]$Markers)

    $latin1 = [System.Text.Encoding]::Latin1
    $utf16 = [System.Text.Encoding]::Unicode
    $leaks = [System.Collections.Generic.List[string]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $Root -File -Recurse) {
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $views = @($latin1.GetString($bytes), $utf16.GetString($bytes))
        if ($bytes.Length -gt 1) { $views += $utf16.GetString($bytes, 1, $bytes.Length - 1 - (($bytes.Length - 1) % 2)) }
        $found = $false
        foreach ($view in $views) {
            foreach ($marker in $Markers) {
                if ($view.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $found = $true; break }
            }
            if ($found) { break }
        }
        if ($found) { $leaks.Add([System.IO.Path]::GetRelativePath($Root, $file.FullName).Replace('\', '/')) }
    }
    , @($leaks | Sort-Object -Unique)
}

function Remove-RepositoryRootFromText {
    <# テキスト中のリポジトリの絶対パス（区切り文字の違い、JSONエスケープを含む）を取り除き、相対パスにする。 #>
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Text, [Parameter(Mandatory)][string]$RepositoryRoot)

    $root = $RepositoryRoot.TrimEnd('\', '/')
    foreach ($variant in @(($root.Replace('\', '\\') + '\\'), ($root + '\'), ($root.Replace('\', '/') + '/'))) {
        $Text = $Text.Replace($variant, '', [StringComparison]::OrdinalIgnoreCase)
    }
    $Text
}
function Resolve-DotNetPath {
    <# 指定があればそれを、なければリポジトリ直下の.dotnet\dotnet.exe（固定SDK）、なければdotnetを返す。 #>
    param([string]$Requested, [Parameter(Mandatory)][string]$RepositoryRoot)
    if (-not [string]::IsNullOrWhiteSpace($Requested)) { return $Requested }
    $local = Join-Path $RepositoryRoot '.dotnet/dotnet.exe'
    if (Test-Path -LiteralPath $local -PathType Leaf) { return $local }
    'dotnet'
}

function Get-ReleaseTagProblem {
    <# HEADを指す注釈付き（annotated）タグ v<Version> であることを確認し、問題があれば文字列を返す。 #>
    param([Parameter(Mandatory)][string]$Version)
    $name = "v$Version"
    if (@(git tag --points-at HEAD) -notcontains $name) {
        return "HEAD does not have the release tag $name. Create the tag on the verified commit first."
    }
    if ((git cat-file -t "refs/tags/$name" 2>$null) -ne 'tag') {
        return "The release tag $name must be an annotated tag (git tag -a $name -m '...'), not a lightweight tag."
    }
    $null
}

function Find-VerificationEvidence {
    <#
      artifacts/verify 内で、指定commitに対する最新の「リリースに使える」証跡を返す。
      後から実行した失敗・試作の証跡が、以前の合格した証跡を隠さないようにする。
      合格する証跡がなければ、最新の証跡の問題点を含む例外を投げる。
    #>
    param(
        [Parameter(Mandatory)][string]$EvidenceRoot,
        [Parameter(Mandatory)][string]$SourceCommit,
        [int]$MaxAgeHours = 24
    )
    $prefix = $SourceCommit.Substring(0, 8)
    $directories = @(Get-ChildItem -LiteralPath $EvidenceRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object Name -Like "$prefix-*" | Sort-Object Name -Descending)
    if ($directories.Count -eq 0) {
        throw "No verification evidence for commit $prefix. Run scripts/verify-local.ps1 first."
    }
    $newestProblems = $null
    foreach ($directory in $directories) {
        $file = Join-Path $directory.FullName 'verification.json'
        $summary = Join-Path $directory.FullName 'test-summary.json'
        if (-not ((Test-Path -LiteralPath $file -PathType Leaf) -and (Test-Path -LiteralPath $summary -PathType Leaf))) {
            if ($null -eq $newestProblems) { $newestProblems = "Verification evidence is incomplete: $($directory.Name)" }
            continue
        }
        $evidence = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
        $problems = Test-VerificationEvidence -Evidence $evidence -SourceCommit $SourceCommit -MaxAgeHours $MaxAgeHours
        if ($problems.Count -eq 0) {
            return [pscustomobject]@{ Directory = $directory.FullName; Evidence = $evidence }
        }
        if ($null -eq $newestProblems) { $newestProblems = "Verification evidence rejected ($($directory.Name)): $($problems -join '; ')" }
    }
    throw "$newestProblems. Re-run scripts/verify-local.ps1."
}

function Get-TrxSummary {
    <# TRX（Visual Studio Test Results）の集計値を返す。件数が読めない場合は例外。 #>
    param([Parameter(Mandatory)][string]$Path)

    [xml]$xml = Get-Content -LiteralPath $Path -Raw
    $counters = $xml.SelectSingleNode("//*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw "TRX has no result counters: $([System.IO.Path]::GetFileName($Path))"
    }
    $value = { param($name) if ($null -ne $counters.$name) { [int]$counters.$name } else { 0 } }
    $failed = (& $value 'failed') + (& $value 'error') + (& $value 'timeout') + (& $value 'aborted')
    [ordered]@{
        total = & $value 'total'
        executed = & $value 'executed'
        passed = & $value 'passed'
        failed = $failed
        skipped = & $value 'notExecuted'
    }
}

function Get-VulnerablePackageReport {
    <#
      `dotnet list package --vulnerable --include-transitive --format json` の出力を解析する。
      Packages: 脆弱性を持つパッケージ（直接・推移的）。Problems: 脆弱性データの取得失敗などの問題。
    #>
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Json)

    $start = $Json.IndexOf('{')
    if ($start -lt 0) {
        throw 'The vulnerability report does not contain JSON.'
    }
    $report = $Json.Substring($start) | ConvertFrom-Json
    $packages = [System.Collections.Generic.List[object]]::new()
    $problems = [System.Collections.Generic.List[string]]::new()
    foreach ($problem in @($report.problems)) {
        if ($null -ne $problem) {
            $problems.Add("$($problem.level): $($problem.text)")
        }
    }
    foreach ($project in @($report.projects)) {
        foreach ($framework in @($project.frameworks)) {
            foreach ($group in 'topLevelPackages', 'transitivePackages') {
                foreach ($package in @($framework.$group)) {
                    # 注意: @($null).Count は 1 になるため、nullを先に除外する。
                    if ($null -ne $package -and $null -ne $package.vulnerabilities -and @($package.vulnerabilities).Count -gt 0) {
                        $packages.Add([ordered]@{
                                project = [System.IO.Path]::GetFileNameWithoutExtension([string]$project.path)
                                id = $package.id
                                resolvedVersion = $package.resolvedVersion
                                severities = @($package.vulnerabilities | ForEach-Object { $_.severity })
                                advisories = @($package.vulnerabilities | ForEach-Object { $_.advisoryurl })
                            })
                    }
                }
            }
        }
    }
    [pscustomobject]@{ Packages = @($packages); Problems = @($problems) }
}

function Test-VerificationEvidence {
    <#
      verify-local.ps1 の証跡がリリースに使えるかを判定し、問題の一覧を返す（空配列なら合格）。
      - 現在のcommitと一致し、成功し、releasable（clean tree）であること
      - テストが1件以上実行され、失敗0であること
      - 脆弱な依存0件、Migration差分なしが確認されていること
      - MaxAgeHours以内に作成されたこと（脆弱性データベースは日々更新されるため）
    #>
    param(
        [Parameter(Mandatory)]$Evidence,
        [Parameter(Mandatory)][string]$SourceCommit,
        [int]$MaxAgeHours = 24,
        [DateTimeOffset]$Now = [DateTimeOffset]::UtcNow
    )

    $problems = [System.Collections.Generic.List[string]]::new()
    if ($Evidence.schemaVersion -ne 1) { $problems.Add('schemaVersion is not 1') }
    if ($Evidence.sourceCommit -ne $SourceCommit) {
        $problems.Add("evidence commit $($Evidence.sourceCommit) differs from HEAD $SourceCommit")
    }
    if ($Evidence.passed -ne $true) {
        $problems.Add("verification did not pass (failed step: $($Evidence.failedStep))")
    }
    if ($Evidence.releasable -ne $true -or $Evidence.workingTreeClean -ne $true) {
        $problems.Add('evidence is not releasable (dirty working tree)')
    }
    if ($Evidence.tests.executed -le 0) { $problems.Add('no tests were executed') }
    if ($Evidence.tests.failed -ne 0) { $problems.Add("$($Evidence.tests.failed) test(s) failed") }
    if ($Evidence.nugetAudit.vulnerablePackageCount -ne 0) { $problems.Add('vulnerable packages were reported') }
    if ($Evidence.migrationCheck.pendingModelChanges -ne $false) {
        $problems.Add('migration/model diff check did not confirm "no changes"')
    }
    if ($Evidence.nugetAudit.mode -ne 'all') { $problems.Add('NuGet audit mode is not "all"') }
    # 集計値だけでなく、必須ステップがすべて実行され成功していることも確認する。
    foreach ($step in $script:RequiredVerificationSteps) {
        $recorded = @($Evidence.steps | Where-Object { $_.name -eq $step })
        if ($recorded.Count -ne 1 -or $recorded[0].status -ne 'Passed') {
            $problems.Add("required step '$step' is missing or did not pass")
        }
    }
    try {
        $age = $Now - [DateTimeOffset]$Evidence.finishedAtUtc
        if ($age.TotalHours -gt $MaxAgeHours) {
            $problems.Add("evidence is older than $MaxAgeHours hours; re-run scripts/verify-local.ps1")
        }
        elseif ($age.TotalMinutes -lt -5) {
            $problems.Add('evidence timestamp is in the future')
        }
    }
    catch {
        $problems.Add('evidence has no valid finishedAtUtc')
    }
    , @($problems)
}
