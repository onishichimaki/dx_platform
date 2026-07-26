[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string]$SiteName,

    [Parameter(Mandatory)]
    [string]$AppPoolName,

    [Parameter(Mandatory)]
    [string]$DeployPath,

    [Parameter(Mandatory)]
    [string]$BackupSource
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

$resolvedDeployPath = Resolve-SafeDeploymentPath -Path $DeployPath -Label 'DeployPath'
$resolvedBackupSource = Resolve-SafeDeploymentPath -Path $BackupSource -Label 'BackupSource'
if ($resolvedDeployPath -eq $resolvedBackupSource) {
    throw 'DeployPath and BackupSource must be different.'
}
if (-not (Test-Path -LiteralPath $resolvedBackupSource -PathType Container)) {
    throw "BackupSource was not found: $resolvedBackupSource"
}

Import-Module WebAdministration

if ($PSCmdlet.ShouldProcess($SiteName, 'Stop IIS site for rollback')) {
    Stop-Website -Name $SiteName -ErrorAction SilentlyContinue
    Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
}

try {
    if (-not (Test-Path -LiteralPath $resolvedDeployPath)) {
        New-Item -ItemType Directory -Path $resolvedDeployPath | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($resolvedDeployPath, 'Replace deployed files from validated backup')) {
        Get-ChildItem -LiteralPath $resolvedDeployPath -Force |
            Remove-Item -Recurse -Force
        Get-ChildItem -LiteralPath $resolvedBackupSource -Force |
            Copy-Item -Destination $resolvedDeployPath -Recurse -Force
    }
}
finally {
    if ($PSCmdlet.ShouldProcess($SiteName, 'Start IIS site after rollback')) {
        Start-WebAppPool -Name $AppPoolName
        Start-Website -Name $SiteName
    }
}

Write-Host "Rollback completed from $resolvedBackupSource"
