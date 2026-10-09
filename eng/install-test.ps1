# Installs the release eng\release.ps1 just built the way a player does, and takes it apart again (owner, 2026-10-09;
# docs/DESIGN.md §8, "Distribution"):
# - the Setup, silently;
# - the installed app's version, Windows' Apps entry and the shortcuts;
# - one start of the installed app, which saves its window and exits (--snapshot: it asks for no update and sends no
#   report);
# - its log;
# - Velopack's uninstall, which must leave the player's data folder.
# The Release workflow runs it on GitHub's runner before anything is attested or published, so a Setup that doesn't
# install, or an app that doesn't start from its install, never reaches players. Its log is public: it prints the app
# log of that one start on the runner, which holds nothing of anyone's.
# It refuses on a PC where Shturmap is installed or has data: it would replace and remove that install.
# Usage: .\eng\install-test.ps1   (after .\eng\release.ps1)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$git = if (Get-Command git -ErrorAction SilentlyContinue) { 'git' } else { 'C:\Program Files\Git\cmd\git.exe' }
$head = (& $git -C $root rev-parse HEAD).Trim()
$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
$setup = Join-Path $root 'artifacts\release\Shturmap-Setup.exe'
# Velopack's id and so the install folder, as eng\release.ps1 packs it (Distribution.PackId).
$packId = 'ShturmapApp'
$install = Join-Path $env:LOCALAPPDATA $packId
$exe = Join-Path $install 'current\Shturmap.exe'
$data = Join-Path $env:LOCALAPPDATA 'Shturmap'
$entryKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$packId"
$shortcuts = @(
  (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Shturmap.lnk'),
  (Join-Path ([Environment]::GetFolderPath('Programs')) 'Shturmap.lnk'))
$snapshot = Join-Path $root 'artifacts\install-test'

function Fail([string] $what) { throw "Install test: $what" }
function Step([string] $what) { Write-Output "- $what" }

# Waits for the process itself only: Start-Process -Wait would also wait for an app the Setup starts.
function RunToEnd([string] $file, [string[]] $arguments, [int] $seconds) {
  $process = Start-Process -FilePath $file -ArgumentList $arguments -PassThru
  $null = $process.Handle  # keeps the exit code readable once it has ended
  if (-not $process.WaitForExit($seconds * 1000)) {
    $process.Kill()
    Fail "$(Split-Path $file -Leaf) $($arguments -join ' ') didn't end within $seconds s."
  }
  $process.ExitCode
}

function Running {
  @(Get-CimInstance Win32_Process -Filter "Name = 'Shturmap.exe'" |
    Where-Object { $_.ExecutablePath -and $_.ExecutablePath.StartsWith($install, [StringComparison]::OrdinalIgnoreCase) })
}

if (-not (Test-Path $setup)) { Fail 'no artifacts\release\Shturmap-Setup.exe: run eng\release.ps1 first.' }
if ((Test-Path $install) -or (Test-Path $data)) {
  Fail "Shturmap is installed or has data on this PC ($install, $data), which this test would replace and remove. It runs on GitHub's runner, in the Release workflow."
}
if (Test-Path $snapshot) { Remove-Item $snapshot -Recurse -Force }

# 1. The Setup, as a player runs it, without its window.
$code = RunToEnd $setup @('--silent') 300
if ($code -ne 0) { Fail "the Setup ended with $code." }
if (-not (Test-Path $exe)) { Fail "no $exe after the Setup." }
$installed = (Get-Item $exe).VersionInfo.ProductVersion
if ($installed -ne "$version+$head") { Fail "the installed app is $installed, not $version+$head." }
Step "Installed $installed in $install"
$entry = Get-ItemProperty $entryKey -ErrorAction SilentlyContinue
if (-not $entry -or $entry.DisplayVersion -ne $version) { Fail "Windows' Apps entry ($entryKey) is missing or not $version." }
foreach ($shortcut in $shortcuts) { if (-not (Test-Path $shortcut)) { Fail "no shortcut $shortcut." } }
Step "Windows lists it under Apps ($($entry.DisplayName) $($entry.DisplayVersion)); shortcuts on the desktop and in the Start menu"

# Velopack may start the app once it has installed it: that run is closed before the test's own start.
$deadline = (Get-Date).AddSeconds(10)
while (-not (Running) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1 }
foreach ($found in Running) {
  $process = Get-Process -Id $found.ProcessId -ErrorAction SilentlyContinue
  if (-not $process) { continue }
  [void]$process.CloseMainWindow()
  if (-not $process.WaitForExit(30000)) { $process.Kill(); $process.WaitForExit() }
  Step 'The Setup started Shturmap; closed it'
}

# 2. One start from the install, with the release's data folder: it draws its window, saves it and exits.
$code = RunToEnd $exe @('--snapshot', "`"$snapshot`"") 180
$log = @(Get-ChildItem (Join-Path $data 'logs') -Filter 'shturmap-*.log' -ErrorAction SilentlyContinue |
  ForEach-Object { Get-Content $_.FullName -Encoding utf8 })
Write-Output '  The app log:'
$log | ForEach-Object { "    $_" }
if ($code -ne 0) { Fail "the installed app ended with $code." }
$window = Join-Path $snapshot 'window.png'
if (-not (Test-Path $window) -or (Get-Item $window).Length -lt 10KB) { Fail "no window in ${snapshot}: the app didn't draw it." }
$start = "Starting Shturmap $version+$($head.Substring(0, 7)) (installed, release data folder)"
if (-not ($log | Where-Object { $_.Contains($start) })) { Fail "the app log doesn't say '$start'." }
$errors = @($log | Where-Object { $_ -match '^\S+ \S+ ERROR ' })
if ($errors) { Fail "the app log has $($errors.Count) ERROR line(s)." }
$crashes = @(Get-ChildItem (Join-Path $data 'crashes') -File -ErrorAction SilentlyContinue)
if ($crashes) { Fail "the app left $($crashes.Count) crash record(s)." }
Step "Started from the install with the release's data folder; window saved ($([int]((Get-Item $window).Length / 1KB)) KB), no ERROR in its log, no crash"

# 3. Velopack's uninstall, as the app starts it ("Uninstall Shturmap…" in settings): the player's data stays.
$code = RunToEnd (Join-Path $install 'Update.exe') @('--silent', 'uninstall') 300
if ($code -ne 0) { Fail "the uninstall ended with $code." }
# Velopack may finish removing the files after Update.exe has ended.
$deadline = (Get-Date).AddSeconds(60)
while ((Test-Path $exe) -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 2 }
if (Test-Path $exe) { Fail "the app is still in $install a minute after the uninstall." }
if (Test-Path $entryKey) { Fail 'Windows still lists Shturmap under Apps.' }
foreach ($shortcut in $shortcuts) { if (Test-Path $shortcut) { Fail "the shortcut $shortcut stayed." } }
if (-not (Get-ChildItem (Join-Path $data 'logs') -Filter 'shturmap-*.log' -ErrorAction SilentlyContinue)) {
  Fail "the uninstall took the player's data folder ($data) with it."
}
Step 'Uninstalled: the app, its shortcuts and its Apps entry are gone; the data folder stays'
Write-Output "Install test passed for $version+$head."
