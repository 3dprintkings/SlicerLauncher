$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "SlicerLauncher\SlicerLauncher.csproj"
$outputDir = Join-Path $root "dist\Portable"
$exe = Join-Path $outputDir "SlicerLauncher.exe"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "The .NET 8 SDK is not installed." -ForegroundColor Red
    Write-Host "Install the .NET 8 SDK and run this script again."
    exit 1
}

Remove-Item $outputDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Write-Host "Building portable SlicerLauncher 1.0.0..." -ForegroundColor Cyan

dotnet restore $project
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $outputDir

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path $exe)) {
    Write-Host "Build completed, but SlicerLauncher.exe was not found." -ForegroundColor Red
    exit 1
}

# Remove any files that are not required for the portable distribution.
Get-ChildItem $outputDir -File | Where-Object { $_.Name -ne "SlicerLauncher.exe" } | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Portable version created successfully:" -ForegroundColor Green
Write-Host $exe
Write-Host ""
Write-Host "Local test build created. Official binary distribution is planned through the Microsoft Store."
