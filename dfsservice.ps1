param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("start", "stop")]
    [string]$Toggle,

    [Parameter(Mandatory = $true)]
    [string]$PortOrAddress,

    [Parameter(Mandatory = $false)]
    [string]$ExtentServiceAddress,

    [Parameter(Mandatory = $false)]
    [string]$LockServiceAddress
)

function Start-DfsService {
    param (
        [string]$Port,
        [string]$ExtentServiceAddress,
        [string]$LockServiceAddress
    )
    if (-not $ExtentServiceAddress -or -not $LockServiceAddress) {
        exit 1
    }

    $startCommand = "dotnet run --project DfsService -- $Port $ExtentServiceAddress $LockServiceAddress"
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $startCommand
}

function Stop-DfsService {
    param (
        [string]$Port
    )
    $processes = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction Stop | Select-Object -ExpandProperty OwningProcess
    if ($processes) {
        foreach ($processId in $processes) {
            Stop-Process -Id $processId -Force
        }
    } 
}

switch ($Toggle) {
    "start" {
        Start-DfsService -Port $PortOrAddress -ExtentServiceAddress $ExtentServiceAddress -LockServiceAddress $LockServiceAddress
    }
    "stop" {
        Stop-DfsService -Port $PortOrAddress
    }
    default {
        Write-Host "Invalid toggle switch value. Allowed values are 'start' or 'stop'."
        exit 1
    }
}