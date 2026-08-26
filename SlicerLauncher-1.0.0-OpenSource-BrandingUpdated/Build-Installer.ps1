$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$portableBuild = Join-Path $root "Build-Portable.ps1"
$iss = Join-Path $root "Installer\SlicerLauncher.iss"
$setup = Join-Path $root "dist\Installer\SlicerLauncher-Setup.exe"

# First create the self-contained portable EXE used by the installer.
& $portableBuild
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

function Find-InnoCompiler {
    $candidates = @(
        "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { return $candidate }
    }

    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    return $null
}

$iscc = Find-InnoCompiler

if (-not $iscc) {
    Write-Host "Inno Setup 6 could not be found." -ForegroundColor Red
    Write-Host "Install Inno Setup 6 manually and run Build-Installer.ps1 again."
    Write-Host "No software will be downloaded or installed automatically by this build script." -ForegroundColor Yellow
    exit 1
}

$installerDir = Join-Path $root "dist\Installer"
Remove-Item $installerDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $installerDir | Out-Null

Write-Host "Building installer..." -ForegroundColor Cyan
& $iscc $iss
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path $setup)) {
    Write-Host "Installer build completed, but the setup EXE was not found." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Installer created successfully:" -ForegroundColor Green
Write-Host $setup
