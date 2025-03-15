# Script starting/stopping Extent Service (for Windows PowerShell)
#
# Input arguments:
#
# 1. Toggle switch to indicate starting or stopping the service (allowed values: start, stop)
# 2. Port where the service will start (e.g. 2001) or port where to kill the service (e.g. 2001)
# 3. Root directory where extent will be stored (e.g. C:\extent-root)
#
# Examples how to use the script:
#
# .\extentservice.ps1 start 2001 C:\extent-root
# .\extentservice.ps1 stop 2001

param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("start", "stop")]
    [string]$Toggle,

    [Parameter(Mandatory = $true)]
    [string]$PortOrAddress,

    [Parameter(Mandatory = $false)]
    [string]$RootDirectory
)

function Start-ExtentService {
    param (
        [string]$Port,
        [string]$RootDirectory
    )
    if (-not $RootDirectory) {
        Write-Host "Root directory is required for starting the service."
        exit 1
    }

    $startCommand = "dotnet run --project ExtentService -- $Port $RootDirectory"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $startCommand
}

function Stop-ExtentService {
    param (
        [string]$Port
    )
    
}

switch ($Toggle) {
    "start" {
        Start-ExtentService -Port $PortOrAddress -RootDirectory $RootDirectory
    }
    "stop" {
        Stop-ExtentService -Port $PortOrAddress
    }
    default {
        Write-Host "Invalid toggle switch value. Allowed values are 'start' or 'stop'."
        exit 1
    }
}