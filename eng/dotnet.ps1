# Runs the .NET 10 SDK even when the machine-wide dotnet on PATH has no SDK
# (this machine has a per-user SDK in %LOCALAPPDATA%\Microsoft\dotnet).
# Usage: .\eng\dotnet.ps1 build | test | run --project src\Shturmap.App
$ErrorActionPreference = 'Stop'
$roots = @($env:DOTNET_ROOT, "$env:LOCALAPPDATA\Microsoft\dotnet", "$env:ProgramFiles\dotnet") |
  Where-Object { $_ -and (Test-Path (Join-Path $_ 'dotnet.exe')) }
foreach ($root in $roots) {
  if (Get-ChildItem (Join-Path $root 'sdk') -Directory -Filter '10.*' -ErrorAction SilentlyContinue) {
    $env:DOTNET_ROOT = $root
    $env:PATH = "$root;$env:PATH"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    & (Join-Path $root 'dotnet.exe') @args
    exit $LASTEXITCODE
  }
}
throw 'No .NET 10 SDK found. Install it with: winget install Microsoft.DotNet.SDK.10'
