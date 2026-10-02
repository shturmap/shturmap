# Builds a self-contained Shturmap into artifacts\Shturmap. Run artifacts\Shturmap\Shturmap.exe; no .NET install needed.
# The licences travel in its licenses\ folder (the app project gathers them, eng\notices.ps1). For the release to
# send, a single exe, see eng\release.ps1.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'artifacts\Shturmap'
# A running copy locks its files; publishing over it half-way leaves new XAML next to old code, which crashes.
$exe = Join-Path $out 'Shturmap.exe'
$running = Get-CimInstance Win32_Process -Filter "Name = 'Shturmap.exe'" | Where-Object { $_.ExecutablePath -eq $exe }
if ($running) {
  Write-Error "Shturmap is running from $out (process $($running.ProcessId -join ', ')). Close it and publish again."
  exit 1
}
& (Join-Path $PSScriptRoot 'dotnet.ps1') publish (Join-Path $root 'src\Shturmap.App\Shturmap.App.csproj') -c Release -o $out -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output "Published to $out"
