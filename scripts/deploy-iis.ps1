[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$PackagePath,

    [Parameter(Mandatory)]
    [string]$SiteName,

    [Parameter(Mandatory)]
    [string]$AppPoolName,

    [Parameter(Mandatory)]
    [string]$DeployPath,

    [Parameter(Mandatory)]
    [string]$BackupPath,

    [Parameter(Mandatory)]
    [ValidatePattern('^https://')]
    [string]$BaseUrl
)

$ErrorActionPreference = 'Stop'

function Resolve-SafeDeploymentPath {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Label)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $root = [System.IO.Path]::GetPathRoot($fullPath)
    if ([string]::IsNullOrWhiteSpace($root) -or $fullPath -eq $root -or $fullPath.Length -lt 8) {
        throw "$Label is too broad or unsafe: $fullPath"
    }

    return $fullPath.TrimEnd('\')
}

$resolvedPackagePath = [System.IO.Path]::GetFullPath($PackagePath)
$resolvedDeployPath = Resolve-SafeDeploymentPath -Path $DeployPath -Label 'DeployPath'
$resolvedBackupRoot = Resolve-SafeDeploymentPath -Path $BackupPath -Label 'BackupPath'
if ($resolvedDeployPath -eq $resolvedBackupRoot) {
    throw 'DeployPath and BackupPath must be different.'
}
$deployPrefix = $resolvedDeployPath + [System.IO.Path]::DirectorySeparatorChar
$backupPrefix = $resolvedBackupRoot + [System.IO.Path]::DirectorySeparatorChar
if ($resolvedBackupRoot.StartsWith($deployPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    $resolvedDeployPath.StartsWith($backupPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'DeployPath and BackupPath must not contain one another.'
}
if (-not (Test-Path -LiteralPath $resolvedPackagePath -PathType Leaf)) {
    throw "Publish package was not found: $resolvedPackagePath"
}
if ([System.IO.Path]::GetExtension($resolvedPackagePath) -ne '.zip') {
    throw 'PackagePath must point to a ZIP artifact.'
}

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupTarget = Join-Path $resolvedBackupRoot $timestamp
$extractPath = Join-Path ([System.IO.Path]::GetTempPath()) "dxplatform-$([guid]::NewGuid().ToString('N'))"
$hasBackup = $false
$deploymentStarted = $false

Import-Module WebAdministration
New-Item -ItemType Directory -Path $resolvedBackupRoot -Force | Out-Null
New-Item -ItemType Directory -Path $backupTarget -Force | Out-Null
New-Item -ItemType Directory -Path $extractPath -Force | Out-Null

try {
    Expand-Archive -LiteralPath $resolvedPackagePath -DestinationPath $extractPath -Force
    if (-not (Test-Path -LiteralPath (Join-Path $extractPath 'DxPlatform.Web.dll') -PathType Leaf) -or
        -not (Test-Path -LiteralPath (Join-Path $extractPath 'web.config') -PathType Leaf)) {
        throw 'Package is not an IIS application artifact. DxPlatform.Web.dll or web.config is missing at the ZIP root.'
    }

    if (Test-Path -LiteralPath $resolvedDeployPath -PathType Container) {
        $existing = Get-ChildItem -LiteralPath $resolvedDeployPath -Force
        if ($existing.Count -gt 0) {
            $existing | Copy-Item -Destination $backupTarget -Recurse -Force
            $hasBackup = $true
        }
    }
    else {
        New-Item -ItemType Directory -Path $resolvedDeployPath | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($SiteName, 'Stop IIS site for deployment')) {
        $deploymentStarted = $true
        Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
        Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    }

    if ($PSCmdlet.ShouldProcess($resolvedDeployPath, 'Replace deployed files with validated package')) {
        Get-ChildItem -LiteralPath $resolvedDeployPath -Force |
            Remove-Item -Recurse -Force
        Get-ChildItem -LiteralPath $extractPath -Force |
            Copy-Item -Destination $resolvedDeployPath -Recurse -Force
    }

    if ($PSCmdlet.ShouldProcess($SiteName, 'Start IIS site after deployment')) {
        Start-WebAppPool -Name $AppPoolName
        Start-Website -Name $SiteName
    }

    if ($deploymentStarted) {
        & (Join-Path $PSScriptRoot 'smoke-test.ps1') -BaseUrl $BaseUrl
        Write-Host "Deployment completed. Backup: $backupTarget"
        Write-Output $backupTarget
    }
}
catch {
    Write-Warning "Deployment failed: $($_.Exception.Message)"
    if ($deploymentStarted -and $hasBackup) {
        & (Join-Path $PSScriptRoot 'rollback-iis.ps1') `
            -SiteName $SiteName `
            -AppPoolName $AppPoolName `
            -DeployPath $resolvedDeployPath `
            -BackupSource $backupTarget `
            -Confirm:$false
    }
    elseif ($deploymentStarted) {
        Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
        Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
    }
    throw
}
finally {
    if (Test-Path -LiteralPath $extractPath) {
        Remove-Item -LiteralPath $extractPath -Recurse -Force
    }
}
