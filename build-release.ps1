param(
    [string]$Configuration = 'Release',
    [string]$Platform = 'ARM64',
    [string]$RuntimeIdentifier = 'win-arm64'
)

$ErrorActionPreference = 'Stop'

function Get-MsBuildPath {
    $cmd = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        throw 'MSBuild was not found. Run this from a Visual Studio Developer PowerShell or install Visual Studio Build Tools.'
    }

    $installPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if (-not $installPath) {
        throw 'Visual Studio with MSBuild was not found.'
    }

    $msbuild = Join-Path $installPath 'MSBuild\Current\Bin\MSBuild.exe'
    if (-not (Test-Path $msbuild)) {
        throw "MSBuild.exe was not found at $msbuild"
    }

    return $msbuild
}

function Invoke-MSBuild {
    param([string[]]$Arguments)

    & $msbuildPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild failed with exit code $LASTEXITCODE. The existing release was left unchanged."
    }
}

$msbuildPath = Get-MsBuildPath
$solution = Join-Path $PSScriptRoot 'DuoCompanion.sln'
$project = Join-Path $PSScriptRoot 'src\DuoCompanion.App\DuoCompanion.App.csproj'
$releaseCheck = Join-Path $PSScriptRoot 'scripts\Test-DuoCompanionRelease.ps1'
$staging = Join-Path $PSScriptRoot 'dist-staging\DuoCompanion-win-arm64'
$release = Join-Path $PSScriptRoot 'dist\DuoCompanion-win-arm64'

if (-not (Test-Path $releaseCheck)) {
    throw "Release check script was not found at $releaseCheck"
}

Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue

Invoke-MSBuild @(
    $solution,
    '/t:Clean',
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:RuntimeIdentifier=$RuntimeIdentifier"
)
Invoke-MSBuild @(
    $solution,
    '/restore',
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:RuntimeIdentifier=$RuntimeIdentifier"
)
Invoke-MSBuild @(
    $project,
    '/restore',
    '/t:Publish',
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/p:RuntimeIdentifier=$RuntimeIdentifier",
    '/p:SelfContained=true',
    "/p:PublishDir=$staging\"
)

Copy-Item $releaseCheck (Join-Path $staging 'Test-DuoCompanionRelease.ps1')

$requiredOutput = @('DuoCompanion.exe', 'coreclr.dll')
$missingOutput = $requiredOutput | Where-Object {
    -not (Test-Path (Join-Path $staging $_))
}
if ($missingOutput) {
    throw "Publish output is not self-contained. Missing: $($missingOutput -join ', '). The existing release was left unchanged."
}

Remove-Item $release -Recurse -Force -ErrorAction SilentlyContinue
Move-Item $staging $release

Write-Host "Release output is in $release"
