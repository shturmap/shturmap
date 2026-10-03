# Builds the release to send: artifacts\Shturmap-<version>-win-x64.exe, one self-contained file that .NET unpacks to
# %TEMP%\.net on its first start (docs/DESIGN.md, "Distribution"), and the same build as a folder, zipped beside it
# for whoever prefers that. Each gets a .sha256. The version comes from Directory.Build.props.
# Usage: .\eng\release.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
$artifacts = Join-Path $root 'artifacts'
$name = "Shturmap-$version-win-x64"
$exe = Join-Path $artifacts "$name.exe"
$folder = Join-Path $artifacts 'Shturmap'

# Neither the folder build nor the single exe may be running: their files are about to be replaced.
$running = Get-CimInstance Win32_Process -Filter "Name LIKE 'Shturmap%'" |
  Where-Object { $_.ExecutablePath -eq $exe -or $_.ExecutablePath -eq (Join-Path $folder 'Shturmap.exe') }
if ($running) {
  Write-Error "Shturmap is running from artifacts (process $($running.ProcessId -join ', ')). Close it and release again."
  exit 1
}

$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
$project = Join-Path $root 'src\Shturmap.App\Shturmap.App.csproj'
# Where reports go: the untracked eng\sentry.dsn (or SHTURMAP_SENTRY_DSN). Without it the release can't send reports.
$dsn = @(& (Join-Path $PSScriptRoot 'sentry-dsn.ps1'))
if (-not $dsn) { Write-Warning 'No eng\sentry.dsn: this release has no Report button (docs/DESIGN.md §8, "Reports").' }

# The folder, from empty, so nothing of an older build ends up in the zip; precompiled, like eng\publish.ps1.
if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
& $dotnet publish $project -c Release -o $folder -p:PublishReadyToRun=true @dsn
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The single exe: everything inside, licences included, compressed. Not precompiled: as one file, precompiling
# builds a 120 MB composite image of the whole framework, and the exe grew from 86 to 110 MB and its unpacked copy
# from 200 to 285 MB to start 0.1 s sooner (measured 2026-10-03; DESIGN.md "Distribution").
$stage = Join-Path $artifacts '.single'
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
& $dotnet publish $project -c Release -o $stage -p:PublishReadyToRun=false -p:PublishSingleFile=true `
  -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true @dsn
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$left = @(Get-ChildItem $stage -File | Where-Object Name -ne 'Shturmap.exe')
if ($left) { throw "The single-file publish left files beside the exe: $($left.Name -join ', ')" }
Move-Item (Join-Path $stage 'Shturmap.exe') $exe -Force
Remove-Item $stage -Recurse -Force

$zip = Join-Path $artifacts "$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($folder, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)

foreach ($file in $exe, $zip) {
  $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
  [IO.File]::WriteAllText("$file.sha256", "$hash  $(Split-Path $file -Leaf)`n")
  Write-Output ("{0}  {1:N0} MB  sha256 {2}" -f (Split-Path $file -Leaf), ((Get-Item $file).Length / 1MB), $hash)
}
