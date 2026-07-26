[CmdletBinding()]
param(
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]{0,63}$')]
    [string]$Version = '0.1.0-rc1',

    [string]$OutputDirectory = 'artifacts/company',

    [string]$DotNetPath = 'dotnet',

    [switch]$NoRestore,

    [switch]$AllowDirtyWorkingTree
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
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
if (Test-Path -LiteralPath $workingDirectory) {
    Remove-Item -LiteralPath $workingDirectory -Recurse -Force
}
if (Test-Path -LiteralPath $releaseDirectory) {
    Remove-Item -LiteralPath $releaseDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $packageRoot, $releaseDirectory -Force | Out-Null

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

    Remove-Item (Join-Path $packageRoot 'app/appsettings.Development.json') -Force -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $packageRoot 'app/appsettings.Example.json') -Force -ErrorAction SilentlyContinue
    Get-ChildItem (Join-Path $packageRoot 'app') -Filter '*.map' -File -Recurse |
        Remove-Item -Force

    $commit = (git rev-parse HEAD).Trim()
    $migrations = Get-ChildItem 'src/DxPlatform.Infrastructure/Persistence/Migrations' -Filter '*.cs' |
        Where-Object { $_.Name -Match '^\d{14}_.+\.cs$' -and $_.Name -NotLike '*.Designer.cs' } |
        ForEach-Object BaseName
    $manifest = [ordered]@{
        packageVersion = $Version
        sourceCommit = $commit
        target = 'Windows Server / IIS / SQL Server'
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        containsSecrets = $false
        databaseApplyMigrationsOnStartup = $false
        migrations = @($migrations)
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
