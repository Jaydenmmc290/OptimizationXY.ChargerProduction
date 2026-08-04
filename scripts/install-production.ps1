[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishedApplicationDirectory,

    [string]$InstallDirectory = "C:\Program Files\OptimizationXY\Charger",

    [string]$DataRoot = "",

    [string]$RuntimeAccount = "",

    [string]$SqlInstanceName = "SQLEXPRESS"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Run this installer from an elevated PowerShell session."
}

$publishedRoot = (Resolve-Path -LiteralPath $PublishedApplicationDirectory).Path
$required = Join-Path $publishedRoot "OptimizationXY.Charger.exe"
if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
    throw "Published application not found: $required"
}

if ([string]::IsNullOrWhiteSpace($DataRoot)) {
    if (Test-Path -LiteralPath "D:\" -PathType Container) {
        $DataRoot = "D:\OptimizationXYData"
    }
    else {
        $DataRoot = Join-Path $env:ProgramData "OptimizationXY\Charger"
    }
}

$DataRoot = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($DataRoot))
$InstallDirectory = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($InstallDirectory))

if ([string]::IsNullOrWhiteSpace($RuntimeAccount)) {
    $RuntimeAccount = [Security.Principal.WindowsIdentity]::GetCurrent().Name
}

$serviceName = if ($SqlInstanceName -eq "MSSQLSERVER") {
    "MSSQLSERVER"
}
else {
    "MSSQL`$$SqlInstanceName"
}

$sqlService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -eq $sqlService) {
    throw "SQL Server service '$serviceName' was not found. Install SQL Server Express instance '$SqlInstanceName' before running this installer."
}

if ($sqlService.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Running) {
    if ($PSCmdlet.ShouldProcess($serviceName, "Start SQL Server service")) {
        Start-Service -Name $serviceName
        $sqlService.WaitForStatus(
            [System.ServiceProcess.ServiceControllerStatus]::Running,
            [TimeSpan]::FromSeconds(30))
    }
}

if ($PSCmdlet.ShouldProcess($InstallDirectory, "Install Optimization XY Charger")) {
    New-Item -ItemType Directory -Force -Path $InstallDirectory | Out-Null
    Copy-Item -Path (Join-Path $publishedRoot "*") -Destination $InstallDirectory -Recurse -Force

    $runtimeDirectories = @(
        $DataRoot,
        (Join-Path $DataRoot "Config"),
        (Join-Path $DataRoot "Config\CameraConfiguration"),
        (Join-Path $DataRoot "Config\Calibration"),
        (Join-Path $DataRoot "Config\Calibration\Archive"),
        (Join-Path $DataRoot "Config\Calibration\Staging"),
        (Join-Path $DataRoot "Config\Setup"),
        (Join-Path $DataRoot "Logs"),
        (Join-Path $DataRoot "SavedScans")
    )

    foreach ($directory in $runtimeDirectories) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }

    [Environment]::SetEnvironmentVariable(
        "OPTIMIZATION_XY_DATA_DIR",
        $DataRoot,
        [EnvironmentVariableTarget]::Machine)

    $settingsDestination = Join-Path $DataRoot "Config\appsettings.json"
    if (-not (Test-Path -LiteralPath $settingsDestination -PathType Leaf)) {
        $templateCandidates = @(
            (Join-Path (Split-Path -Parent $publishedRoot) "ConfigTemplate\appsettings.json"),
            (Join-Path $publishedRoot "appsettings.production.template.json")
        )
        $template = $templateCandidates |
            Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
            Select-Object -First 1

        if ($null -eq $template) {
            throw "Production settings template was not found."
        }

        Copy-Item -LiteralPath $template -Destination $settingsDestination
    }

    # Schema 10 production uses direct Sapera feature configuration.
    # No camera CCF is installed or copied.

    # The runtime account needs Modify access only to the machine data root,
    # never to Program Files or the application binaries.
    $grant = "${RuntimeAccount}:(OI)(CI)M"
    & icacls.exe $DataRoot /grant $grant /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to grant runtime data-folder access to $RuntimeAccount."
    }
}

Write-Host "Application installed to: $InstallDirectory"
Write-Host "Runtime data root: $DataRoot"
Write-Host "Runtime account with data-folder Modify access: $RuntimeAccount"
Write-Host "SQL Server service: $serviceName ($((Get-Service -Name $serviceName).Status))"
Write-Host "Start OptimizationXY.Charger.exe and complete Setup / Calibration."
Write-Host "The settings template remains in Commissioning mode; the installer does not pre-authorize production motion."
