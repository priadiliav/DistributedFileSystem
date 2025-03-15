# Script starting/stopping Lock Service (for Windows PowerShell)
#
# Input arguments:
#
# 1. Toggle switch to indicate starting or stopping the service (allowed values: start, stop)
# 2. Port where the service will start (e.g. 1001) or address where to connect and stop the service (e.g. 127.0.0.1:1001)
#
# Examples how to use the script:
#
# .\lockservice.ps1 start 1001
# .\lockservice.ps1 stop 127.0.0.1:1001

param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("start", "stop")]
    [string]$Toggle,

    [Parameter(Mandatory = $true)]
    [string]$PortOrAddress
)

function Start-LockService {
    param (
        [string]$Port
    )
    $startCommand = "dotnet run --project LockService -- $Port"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $startCommand
}

function Stop-LockService {
    param (
        [string]$Address
    )
    
}

switch ($Toggle) {
    "start" {
        Start-LockService -Port $PortOrAddress
    }
    "stop" {
        Stop-LockService -Address $PortOrAddress
    }
    default {
        Write-Host "Invalid toggle switch value. Allowed values are 'start' or 'stop'."
        exit 1
    }
}