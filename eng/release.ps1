# Builds the release (docs/DESIGN.md §8, "Distribution") into artifacts\release: the app as a folder (app\,
# precompiled, with the Sentry DSN), packed by Velopack's vpk into packages\: the Setup, the full package (and a delta
# from the last GitHub release), the update feed (releases.win.json, RELEASES) and a portable zip. Beside them,
# Shturmap-Setup.exe: the Setup under the name players download, with its .sha256. Nothing is uploaded;
# eng\publish-release.ps1 does that. The Release workflow (.github/workflows/release.yml) runs both on GitHub's runner,
# for every release players get; on the PC this builds for tries and update tests. The dev build is eng\dev.ps1's, in
# artifacts\dev.
# The version comes from Directory.Build.props, the release notes from docs\release-notes\<version>.md.
# A release is built from a clean working tree: the build's version names the commit only, so a build from a changed
# tree would pass for that commit's (the review of 2026-10-04, A39). -AllowDirty builds anyway, for a local try, and
# leaves a note (artifacts\release\built-from.txt) that eng\publish-release.ps1 refuses.
# Usage: .\eng\release.ps1 [-NoDelta] [-AllowDirty]
param(
  # Skip asking GitHub for the last release (no delta package then).
  [switch] $NoDelta,
  # Build although the working tree has changes. Such a build can't be published.
  [switch] $AllowDirty
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$git = if (Get-Command git -ErrorAction SilentlyContinue) { 'git' } else { 'C:\Program Files\Git\cmd\git.exe' }
$head = (& $git -C $root rev-parse HEAD).Trim()
$changed = @(& $git -C $root status --porcelain)
if ($changed -and -not $AllowDirty) {
  throw "The working tree isn't clean, so this build wouldn't be the commit's:`n$($changed -join "`n")`nCommit first, or pass -AllowDirty for a local try."
}
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
# Every -p: is quoted: PowerShell 7 (the Release workflow's shell) passes an unquoted -p:Name=value through
# dotnet.ps1's @args as two arguments, and dotnet takes the second for a project (MSB1008). Windows PowerShell joins them.
if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
& $dotnet publish $project -c Release -o $folder '-p:PublishReadyToRun=true' @dsn
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

# What this build was made from, for eng\publish-release.ps1: the commit, and whether the tree was the commit's.
[IO.File]::WriteAllText((Join-Path $out 'built-from.txt'), "$head $(if ($changed) { 'changed' } else { 'clean' })`n")

# The Setup under the name players look for; the one in packages\ keeps vpk's name for the upload.
$setup = Join-Path $out 'Shturmap-Setup.exe'
Copy-Item (Join-Path $releases "$packId-win-Setup.exe") $setup -Force
foreach ($file in @($setup) + @(Get-ChildItem $releases -File | Where-Object Extension -in '.exe', '.zip', '.nupkg' | ForEach-Object FullName)) {
  $hash = (Get-FileHash $file -Algorithm SHA256).Hash.ToLowerInvariant()
  if ($file -eq $setup) { [IO.File]::WriteAllText("$file.sha256", "$hash  $(Split-Path $file -Leaf)`n") }
  Write-Output ("{0}  {1:N1} MB  sha256 {2}" -f (Split-Path $file -Leaf), ((Get-Item $file).Length / 1MB), $hash)
}
