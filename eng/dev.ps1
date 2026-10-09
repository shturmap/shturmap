# Builds the dev build (docs/DESIGN.md §8, "Developer aids"): Shturmap in dev mode ("Shturmap DEV", the cyan icon, the
# developer tools, its own data folder %LOCALAPPDATA%\Shturmap-dev), packed by Velopack as "ShturmapDev" into a local
# update feed, artifacts\dev\feed. The installed dev app takes each new build from that feed: run this, and its next
# start applies it, never during a raid. The first run installs it (%LOCALAPPDATA%\ShturmapDev, shortcuts "Shturmap
# DEV"); artifacts\dev\Shturmap-DEV-Setup.exe installs it on purpose. It never asks GitHub and never touches the
# release's install (ShturmapApp) or the player's data (Shturmap).
# Each build carries dev\changelog.json: the commits since the last release tag, so the dev view can say what is live.
# Usage: .\eng\dev.ps1 [-WithReports] [-NoInstall]
param(
  # Reports from the dev build go to Sentry too (eng\sentry.dsn), filed under the environment "dev".
  [switch] $WithReports,
  # Build and pack only; don't install the dev build when it isn't installed yet.
  [switch] $NoInstall
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$git = if (Get-Command git -ErrorAction SilentlyContinue) { 'git' } else { 'C:\Program Files\Git\cmd\git.exe' }
$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
$project = Join-Path $root 'src\Shturmap.App\Shturmap.App.csproj'
$dev = Join-Path $root 'artifacts\dev'
$app = Join-Path $dev 'app'
$feed = Join-Path $dev 'feed'
# Velopack's id for the dev build: its own install folder, shortcuts and feed. Keep it equal to Distribution.DeveloperPackId
# (src\Shturmap.Session\Updates.cs).
$packId = 'ShturmapDev'
# Each build must be newer than the last for the installed dev app to take it: the release's version with a
# "dev.<UTC time>" pre-release part, e.g. 0.2.0-dev.261003140512.
$base = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
$version = "$base-dev.$([DateTime]::UtcNow.ToString('yyMMddHHmmss'))"

$running = Get-CimInstance Win32_Process -Filter "Name LIKE 'Shturmap%'" |
  Where-Object { $_.ExecutablePath -eq (Join-Path $app 'Shturmap.exe') }
if ($running) {
  Write-Error "Shturmap is running from $app (process $($running.ProcessId -join ', ')). Close it and build again."
  exit 1
}

# Not precompiled: dev builds come often, and their deltas stay small.
$dsn = @(if ($WithReports) { & (Join-Path $PSScriptRoot 'sentry-dsn.ps1') })
if ($WithReports -and -not $dsn) { Write-Warning 'No eng\sentry.dsn: this dev build sends no reports.' }
if (Test-Path $app) { Remove-Item $app -Recurse -Force }
New-Item -ItemType Directory -Force $feed | Out-Null
& $dotnet publish $project -c Release -o $app '-p:ShturmapDev=true' "-p:Version=$version" @dsn
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# What changed: the commits since the last release tag (v*) that this build contains, else the last 50; newest first.
# Only the hash, the date and the commit's title, which are public in the repository anyway: no authors, no bodies.
$head = (& $git -C $root rev-parse HEAD).Trim()
$tag = @(& $git -C $root tag --list 'v*' --merged HEAD --sort=-creatordate) | Select-Object -First 1
$range = @(if ($tag) { "$tag..HEAD" } else { '-n', '50' })
$commits = @(& $git -C $root log $range '--format=%h%x09%cI%x09%s') | Where-Object { $_ } | ForEach-Object {
  $hash, $date, $subject = $_ -split "`t", 3
  [ordered]@{ hash = $hash; date = $date; subject = ($subject -replace '[A-Za-z]:\\Users\\[^\\\s]+', '%USERPROFILE%') }
}
$changelog = [ordered]@{ build = $head; built = [DateTime]::UtcNow.ToString('o'); commits = @($commits) }
New-Item -ItemType Directory -Force (Join-Path $app 'dev') | Out-Null
[IO.File]::WriteAllText((Join-Path $app 'dev\changelog.json'), (ConvertTo-Json $changelog -Depth 4), (New-Object Text.UTF8Encoding($false)))

# vpk is a pinned local tool (.config\dotnet-tools.json). The feed keeps the earlier packages, so vpk builds a delta
# from the last one.
Push-Location $root
try {
  & $dotnet tool restore | Out-Null
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  & $dotnet vpk pack --packId $packId --packVersion $version --runtime 'win-x64' --packDir $app --mainExe 'Shturmap.exe' `
    --packTitle 'Shturmap DEV' --packAuthors 'the Shturmap contributors' `
    --icon (Join-Path $root 'src\Shturmap.App\Assets\Shturmap-dev.ico') --outputDir $feed
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
  Pop-Location
}

# Keep the feed small: the three newest versions' packages (a full one each, and the deltas between them), and only
# those in the feed's lists (releases.win.json, RELEASES), so the app never asks for a file that is gone.
$keep = Get-ChildItem $feed -Filter "$packId-*-full.nupkg" | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 3
$keepVersions = @($keep | ForEach-Object { $_.Name -replace "^$packId-(.+)-full\.nupkg$", '$1' })
Get-ChildItem $feed -Filter "$packId-*.nupkg" | Where-Object {
  ($_.Name -replace "^$packId-(.+)-(full|delta)\.nupkg$", '$1') -notin $keepVersions
} | Remove-Item -Force
$present = @(Get-ChildItem $feed -Filter '*.nupkg' | ForEach-Object Name)
$utf8 = New-Object Text.UTF8Encoding($false)
$list = Join-Path $feed 'releases.win.json'
$json = Get-Content $list -Raw | ConvertFrom-Json
$json.Assets = @($json.Assets | Where-Object { $_.FileName -in $present })
[IO.File]::WriteAllText($list, (ConvertTo-Json $json -Depth 5 -Compress), $utf8)
$legacy = Join-Path $feed 'RELEASES'
if (Test-Path $legacy) {
  $lines = @(Get-Content $legacy | Where-Object { ($_ -split ' ')[1] -in $present })
  [IO.File]::WriteAllText($legacy, (($lines -join "`n") + "`n"), $utf8)
}

$setup = Join-Path $dev 'Shturmap-DEV-Setup.exe'
Copy-Item (Join-Path $feed "$packId-win-Setup.exe") $setup -Force

# The first time: install it (per user, no admin), quietly. Later builds reach it through the feed.
$install = Join-Path $env:LOCALAPPDATA $packId
$installed = Test-Path (Join-Path $install 'Update.exe')
if (-not $installed -and -not $NoInstall) {
  $setupRun = Start-Process -FilePath $setup -ArgumentList '--silent' -PassThru -Wait
  if ($setupRun.ExitCode -ne 0) { Write-Error "The dev Setup ended with $($setupRun.ExitCode)." }
  $installed = $true
  Write-Output "Installed the dev build: %LOCALAPPDATA%\$packId, shortcuts 'Shturmap DEV'."
}
elseif ($installed) {
  Write-Output 'The installed dev build takes this one at its next start (or RESTART NOW in its rail).'
}
# Where the installed dev app looks for new builds: this feed, written beside its current\ (which Velopack replaces on
# each update) on every run, so it follows whichever checkout built last. Nothing of this PC is compiled in.
if ($installed) {
  [IO.File]::WriteAllText((Join-Path $install 'dev-feed.txt'), $feed, (New-Object Text.UTF8Encoding($false)))
}
Write-Output "Dev build $version from $($head.Substring(0, 7)): $feed ($($commits.Count) changes in its changelog)"
