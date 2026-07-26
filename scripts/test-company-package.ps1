[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackageRoot
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($PackageRoot)
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "PackageRoot was not found: $root"
}

$required = @(
    'app/DxPlatform.Web.dll',
    'app/web.config',
    'database/dxplatform-migrations.sql',
    'config/appsettings.Production.template.json',
    'docs/COMPANY_SETUP_GUIDE.md',
    'manifest.json',
    'SHA256SUMS.txt'
)
foreach ($relative in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf)) {
        throw "Required package file is missing: $relative"
    }
}

$checksumFile = Join-Path $root 'SHA256SUMS.txt'
$listedPaths = [System.Collections.Generic.HashSet[string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
$failures = foreach ($line in Get-Content -LiteralPath $checksumFile) {
    if ($line -notmatch '^([0-9A-Fa-f]{64})  (.+)$') {
        "Invalid checksum line: $line"
        continue
    }
    $expected = $Matches[1].ToUpperInvariant()
    $relative = $Matches[2].Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $null = $listedPaths.Add($relative)
    $target = [System.IO.Path]::GetFullPath((Join-Path $root $relative))
    $rootPrefix = $root.TrimEnd('\') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $target.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        "Checksum path escaped package root: $relative"
        continue
    }
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
        "Checksum target is missing: $relative"
        continue
    }
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($actual -ne $expected) {
        "Checksum mismatch: $relative"
    }
}
$unlistedFiles = Get-ChildItem -LiteralPath $root -File -Recurse |
    Where-Object FullName -ne $checksumFile |
    Where-Object {
        $relative = [System.IO.Path]::GetRelativePath($root, $_.FullName)
        -not $listedPaths.Contains($relative)
    }
if ($unlistedFiles) {
    $failures = @($failures) + @($unlistedFiles | ForEach-Object {
        "File is not covered by checksum manifest: $([System.IO.Path]::GetRelativePath($root, $_.FullName))"
    })
}
if ($failures) {
    throw ($failures -join [Environment]::NewLine)
}

$manifest = Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw | ConvertFrom-Json
if ($manifest.containsSecrets -ne $false -or $manifest.databaseApplyMigrationsOnStartup -ne $false) {
    throw 'Package manifest does not satisfy the company safety policy.'
}

Write-Host "Package validation succeeded: version $($manifest.packageVersion), commit $($manifest.sourceCommit)"
