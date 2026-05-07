param(
    [string]$ServiceName = "OpenIdentityStack",
    [string]$DisplayName = "OpenIdentityStack",
    [string]$Description = "OpenIdentityStack identity and access management service",
    [string]$InstallPath = (Join-Path $PSScriptRoot "..\publish\OpenIdentityStack.Api.exe"),
    [string]$Url = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this script from an elevated PowerShell session."
}

$resolvedPath = Resolve-Path -LiteralPath $InstallPath
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    throw "Service '$ServiceName' already exists. Run uninstall-open-identity-stack.ps1 first."
}

[Environment]::SetEnvironmentVariable("ASPNETCORE_URLS", $Url, "Machine")
New-Service -Name $ServiceName -BinaryPathName "`"$resolvedPath`"" -DisplayName $DisplayName -StartupType Automatic -Description $Description
Write-Host "Installed $ServiceName. Configure appsettings.Production.json and connection strings before starting the service."
