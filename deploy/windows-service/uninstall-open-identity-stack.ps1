param(
    [string]$ServiceName = "OpenIdentityStack"
)

$ErrorActionPreference = "Stop"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell session."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $service) {
    Write-Host "Service '$ServiceName' is not installed."
    return
}

if ($service.Status -ne "Stopped") {
    Stop-Service -Name $ServiceName -Force -ErrorAction Stop
}

sc.exe delete $ServiceName | Out-Null
Write-Host "Uninstalled $ServiceName."
