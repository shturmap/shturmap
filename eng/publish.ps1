# Builds a self-contained Spotter into artifacts\Spotter. Run artifacts\Spotter\Spotter.exe; no .NET install needed.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts\Spotter'
& (Join-Path $PSScriptRoot 'dotnet.ps1') publish (Join-Path $root 'src\Spotter.App\Spotter.App.csproj') -c Release -o $out -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output "Published to $out"
