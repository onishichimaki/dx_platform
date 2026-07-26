[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^https?://')]
    [string]$BaseUrl,

    [ValidateRange(1, 120)]
    [int]$TimeoutSeconds = 15,

    [ValidateRange(1, 10)]
    [int]$RetryCount = 5
)

$ErrorActionPreference = 'Stop'
$readyUrl = "$($BaseUrl.TrimEnd('/'))/health/ready"

for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
    try {
        $response = Invoke-WebRequest `
            -Uri $readyUrl `
            -UseBasicParsing `
            -TimeoutSec $TimeoutSeconds
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
            Write-Host "Smoke Test succeeded: $readyUrl ($($response.StatusCode))"
            return
        }
    }
    catch {
        if ($attempt -eq $RetryCount) {
            throw "Smoke Test failed after $RetryCount attempts: $readyUrl"
        }
    }

    Start-Sleep -Seconds ([Math]::Min($attempt * 2, 10))
}

throw "Smoke Test failed: $readyUrl"
