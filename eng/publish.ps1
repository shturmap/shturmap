# Builds a self-contained Spotter into artifacts\Spotter. Run artifacts\Spotter\Spotter.exe; no .NET install needed.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts\Spotter'
# A running copy locks its files; publishing over it half-way leaves new XAML next to old code, which crashes.
$exe = Join-Path $out 'Spotter.exe'
$running = Get-CimInstance Win32_Process -Filter "Name = 'Spotter.exe'" | Where-Object { $_.ExecutablePath -eq $exe }
if ($running) {
  Write-Error "Spotter is running from $out (process $($running.ProcessId -join ', ')). Close it and publish again."
  exit 1
}
& (Join-Path $PSScriptRoot 'dotnet.ps1') publish (Join-Path $root 'src\Spotter.App\Spotter.App.csproj') -c Release -o $out -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output "Published to $out"
