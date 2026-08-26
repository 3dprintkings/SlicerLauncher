$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $root "Build-Portable.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

& (Join-Path $root "Build-Installer.ps1")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "====================================================" -ForegroundColor Green
Write-Host "Legacy local test builds are ready." -ForegroundColor Green
Write-Host "====================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Portable:"
Write-Host (Join-Path $root "dist\Portable\SlicerLauncher.exe")
Write-Host ""
Write-Host "Installer:"
Write-Host (Join-Path $root "dist\Installer\SlicerLauncher-Setup.exe")
