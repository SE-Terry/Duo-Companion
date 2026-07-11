$ErrorActionPreference = 'Stop'

$releaseDirectory = $PSScriptRoot
$requiredFiles = @(
    'DuoCompanion.exe',
    'coreclr.dll',
    'DuoCompanion.deps.json',
    'DuoCompanion.runtimeconfig.json',
    'Microsoft.WinUI.dll'
)

$missingFiles = $requiredFiles | Where-Object {
    -not (Test-Path (Join-Path $releaseDirectory $_))
}

if ($missingFiles) {
    Write-Error "Release check failed. Missing required files: $($missingFiles -join ', ')"
    exit 1
}

$depsPath = Join-Path $releaseDirectory 'DuoCompanion.deps.json'
$deps = Get-Content $depsPath -Raw
$windowsAppSdk = [regex]::Match($deps, '"Microsoft\.WindowsAppSDK/([^"/]+)"')
if (-not $windowsAppSdk.Success) {
    Write-Error 'Release check failed. Microsoft.WindowsAppSDK was not found in DuoCompanion.deps.json.'
    exit 1
}

$windowsAppSdkVersion = $windowsAppSdk.Groups[1].Value
if (-not $windowsAppSdkVersion.StartsWith('2.2.')) {
    Write-Error "Release check failed. Expected Windows App SDK 2.2.x, found $windowsAppSdkVersion."
    exit 1
}

$runtimeConfigPath = Join-Path $releaseDirectory 'DuoCompanion.runtimeconfig.json'
$runtimeConfig = Get-Content $runtimeConfigPath -Raw | ConvertFrom-Json
$framework = $runtimeConfig.runtimeOptions.framework

Write-Host 'Duo Companion release check passed.' -ForegroundColor Green
Write-Host "Bundled .NET runtime: $($framework.name) $($framework.version)"
Write-Host "Bundled Windows App SDK: $windowsAppSdkVersion"
Write-Host 'Windows App Runtime installation is not required for this self-contained release.'

$installedRuntime = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'Microsoft.WindowsAppRuntime*' } |
    Select-Object Name, Version, Architecture

if ($installedRuntime) {
    Write-Host 'Installed Windows App Runtime packages:'
    $installedRuntime | Format-Table -AutoSize
}
else {
    Write-Host 'Installed Windows App Runtime packages: none found (this is OK).'
}
