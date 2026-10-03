# Builds the release (docs/DESIGN.md §8, "Distribution") into artifacts\release: the app as a folder (app\,
# precompiled, with the Sentry DSN), packed by Velopack's vpk into packages\: the Setup, the full package (and a delta
# from the last GitHub release), the update feed (releases.win.json, RELEASES) and a portable zip. Beside them,
# Shturmap-Setup.exe: the Setup under the name players download, with its .sha256. Nothing is uploaded;
# eng\publish-release.ps1 does that. The dev build is eng\dev.ps1's, in artifacts\dev.
# The version comes from Directory.Build.props, the release notes from docs\release-notes\<version>.md.
# Usage: .\eng\release.ps1 [-NoDelta]
param(
  # Skip asking GitHub for the last release (no delta package then).
  [switch] $NoDelta
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
$out = Join-Path $root 'artifacts\release'
$folder = Join-Path $out 'app'
$releases = Join-Path $out 'packages'
# Velopack's id, and so the install folder %LOCALAPPDATA%\ShturmapApp, which an uninstall deletes: never "Shturmap",
# the data folder. Keep it equal to Distribution.PackId (src\Shturmap.Session\Updates.cs; a test checks).
$packId = 'ShturmapApp'
$repo = 'https://github.com/shturmap/shturmap'
$notes = Join-Path $root "docs\release-notes\$version.md"
if (-not (Test-Path $notes)) { throw "No release notes for $version (docs\release-notes\$version.md)." }

# The folder build may not be running: its files are about to be replaced.
$running = Get-CimInstance Win32_Process -Filter "Name LIKE 'Shturmap%'" |
  Where-Object { $_.ExecutablePath -eq (Join-Path $folder 'Shturmap.exe') }
if ($running) {
  Write-Error "Shturmap is running from $folder (process $($running.ProcessId -join ', ')). Close it and release again."
  exit 1
}

$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
$project = Join-Path $root 'src\Shturmap.App\Shturmap.App.csproj'
# Where reports go: the untracked eng\sentry.dsn (or SHTURMAP_SENTRY_DSN). Without it the release can't send reports.
$dsn = @(& (Join-Path $PSScriptRoot 'sentry-dsn.ps1'))
if (-not $dsn) { Write-Warning 'No eng\sentry.dsn: this release has no Report button (docs/DESIGN.md §8, "Reports").' }

# The folder, from empty, so nothing of an older build is packed; precompiled, like eng\publish.ps1.
if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
& $dotnet publish $project -c Release -o $folder -p:PublishReadyToRun=true @dsn
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# vpk is a pinned local tool (.config\dotnet-tools.json).
Push-Location $root
try {
  & $dotnet tool restore | Out-Null
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

  if (Test-Path $releases) { Remove-Item $releases -Recurse -Force }
  New-Item -ItemType Directory -Force $releases | Out-Null
  # The last release on GitHub (pre-releases included, as the app's update check), so vpk can build a delta from it.
  # The first release has none; a private repository needs a token (GITHUB_TOKEN).
  if (-not $NoDelta) {
    $token = @(if ($env:GITHUB_TOKEN) { '--token', $env:GITHUB_TOKEN })
    & $dotnet vpk download github --repoUrl $repo --pre --outputDir $releases @token
    if ($LASTEXITCODE -ne 0) { Write-Warning 'No earlier release found on GitHub: this release has no delta package.' }
  }

  & $dotnet vpk pack --packId $packId --packVersion $version --runtime 'win-x64' --packDir $folder --mainExe 'Shturmap.exe' `
    --packTitle 'Shturmap' --packAuthors 'the Shturmap contributors' --icon (Join-Path $root 'src\Shturmap.App\Assets\Shturmap.ico') `
    --releaseNotes $notes --outputDir $releases
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
  Pop-Location
}

# The Setup under the name players look for; the one in packages\ keeps vpk's name for the upload.
$setup = Join-Path $out 'Shturmap-Setup.exe'
Copy-Item (Join-Path $releases "$packId-win-Setup.exe") $setup -Force
foreach ($file in @($setup) + @(Get-ChildItem $releases -File | Where-Object Extension -in '.exe', '.zip', '.nupkg' | ForEach-Object FullName)) {
  $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($file -eq $setup) { [IO.File]::WriteAllText("$file.sha256", "$hash  $(Split-Path $file -Leaf)`n") }
  Write-Output ("{0}  {1:N1} MB  sha256 {2}" -f (Split-Path $file -Leaf), ((Get-Item $file).Length / 1MB), $hash)
}
