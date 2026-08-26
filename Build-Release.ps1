$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root "Build-Portable.ps1")
exit $LASTEXITCODE
