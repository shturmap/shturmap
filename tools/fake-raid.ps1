# Plays a scripted Streets raid against Shturmap without the game, and saves window/map snapshots mid-raid.
# Usage: .\tools\fake-raid.ps1 -Exe artifacts\Shturmap\Shturmap.exe -Out <folder for PNGs>
# With -Demo it records the website's hero clip instead: the app plays its scripted interaction (src\Shturmap.App\
# Demo.cs), this script writes the one screenshot that follows the demo's key press, and tools\record-window records
# Shturmap's own window into <Out>\capture.mkv; <Out>\cut.txt holds the loop's in and out points in seconds.
param(
  [Parameter(Mandatory)] [string] $Exe,
  [Parameter(Mandatory)] [string] $Out,
  [int] $SnapshotAfter = 16,
  # Part of a quest name: highlight it, hold its card and pin it before the snapshot.
  [string] $ShowQuest,
  # Play the raid as a Scav: the match setup names another profile than the menu's.
  [switch] $Scav,
  # A PvE-style raid hosted locally: no match-setup line, so the logs can't tell the side.
  [switch] $LocalRaid,
  # No raid at all: the quests start and the app stays in Plan (with two Customs quests, so Plan has folded cards).
  [switch] $PlanOnly,
  # Window size for the snapshot, e.g. 1600x900 (website media); maximised when empty.
  [string] $Window,
  # Culture for dates and numbers in the snapshot.
  [string] $Culture = 'en-US',
  # Snapshot pixel density: 2 renders twice the pixels, for sharp images on high-DPI screens.
  [int] $Scale = 1,
  # Record the hero clip (see above) instead of taking snapshots.
  [switch] $Demo,
  # Part of the name of the quest the demo points at.
  [string] $DemoQuest = 'Road Closed',
  # How long the recorder runs, in seconds; the loop is cut out of it.
  [int] $RecordSeconds = 24,
  # ffmpeg, if it isn't on PATH (winget install --id Gyan.FFmpeg -e).
  [string] $Ffmpeg
)
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ("shturmap-fake-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$start = Get-Date
$session = 'log_' + $start.ToString('yyyy.MM.dd_HH-mm-ss') + '_1.1.5.1.47510'
New-Item -ItemType Directory -Force (Join-Path $root "Logs\$session"), (Join-Path $root 'Screenshots') | Out-Null
$log = Join-Path $root ("Logs\$session\" + $start.ToString('yyyy.MM.dd_HH-mm-ss') + '_1.1.5.1.47510 application_000.log')
function Log([string]$message) {
  $line = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff') + "|1.1.5.1.47510|Info|application|$message`r`n"
  [IO.File]::AppendAllText($log, $line)
}
# Quests come only from the game's notification log, so the fake game "starts" some Streets quests there.
$push = Join-Path $root ("Logs\$session\" + $start.ToString('yyyy.MM.dd_HH-mm-ss') + '_1.1.5.1.47510 push-notifications_000.log')
function QuestStarted([string]$id) {
  $now = Get-Date
  $dt = [DateTimeOffset]::new($now).ToUnixTimeSeconds()
  $body = "{`r`n  `"type`": `"new_message`",`r`n  `"eventId`": `"fake-$id`",`r`n  `"dialogId`": `"54cb50c76803fa8b248b4571`",`r`n" +
    "  `"message`": {`r`n    `"_id`": `"fake-$id`",`r`n    `"type`": 10,`r`n    `"dt`": $dt,`r`n    `"text`": `"quest started`",`r`n" +
    "    `"templateId`": `"$id description`"`r`n  }`r`n}`r`n"
  [IO.File]::AppendAllText($push, $now.ToString('yyyy-MM-dd HH:mm:ss.fff') + "|1.1.5.1.47510|Info|push-notifications|Got notification | ChatMessageReceived`r`n" + $body)
}
function Shot([string]$position, [int]$n = 0) {
  $name = (Get-Date).ToString('yyyy-MM-dd[HH-mm]') + "_$position ($n).png"
  [IO.File]::WriteAllText((Join-Path $root "Screenshots\$name"), '')
}
# Two places on Streets: A, then B near the Primorsky Ave taxi extract.
$posA = '-60.00, 3.50, 300.00_-0.02500, 0.23500, -0.00500, -0.97150_6.45'
$posB = '40.00, 2.50, 120.00_0.01000, 0.99900, -0.04000, 0.02000_14.13'
# The demo's walk: south on Primorsky Ave, facing the way it goes.
$walkFrom = '13.02, 3.92, 381.30_0.00500, 0.04500, -0.00050, 0.99900_14.13'
$walkTo = '12.50, 4.00, 410.00_0.00500, 0.04500, -0.00050, 0.99900_14.15'
function StartRaid {
  Log 'scene preset path:maps/city_preset.bundle rcid:city.scenespreset.asset'
  $raidProfile = if ($Scav) { '000000000000000000000004' } else { '000000000000000000000003' }
  if (-not $LocalRaid) {
    Log "TRACE-NetworkGameCreate profileStatus: 'Profileid: $raidProfile, Status: Busy, RaidMode: Online, Location: TarkovStreets, shortId: FAKE01'"
  }
  Log 'GameStarting:80.26(1.7) real:95.46(2.73) diff:15.19'
  Start-Sleep -Seconds 1
  Log 'GameStarted:90.6(10.33) real:107.49(12.02) diff:16.89'
}

Log 'Session mode: Pve'
Log 'PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0'
# Revision - Streets of Tarkov, Dandies, Ballet Lover, Audit, Glory to CPSU, Road Closed; Shortage and Acquaintance
# want items found in raid (what a Scav raid can do for them).
foreach ($quest in '639135f286e646067c176a87', '65734c186dc1e402c80dc19e', '639135a7e705511c8a4a1b78',
                   '638fcd23dc65553116701d33', '64f5aac4b63b74469b6c14c2', '639282134ed9512be67647ed',
                   '5967733e86f774602332fc84', '5d24b81486f77439c92d6ba8') {
  QuestStarted $quest
}
if ($PlanOnly) {
  # Pharmacist and Golden Swag, on Customs.
  QuestStarted '5969f9e986f7741dde183a50'
  QuestStarted '5979eee086f774311955e614'
}
if ($Demo) {
  $ffmpegExe = if ($Ffmpeg) { $Ffmpeg } elseif (Get-Command ffmpeg -ErrorAction SilentlyContinue) { (Get-Command ffmpeg).Source } else {
    Get-ChildItem (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages') -Recurse -Filter ffmpeg.exe -ErrorAction SilentlyContinue |
      Select-Object -First 1 -ExpandProperty FullName }
  if (-not $ffmpegExe) { throw 'ffmpeg not found: winget install --id Gyan.FFmpeg -e, or pass -Ffmpeg.' }
  $repo = Split-Path $PSScriptRoot -Parent
  & (Join-Path $repo 'eng\dotnet.ps1') build (Join-Path $PSScriptRoot 'record-window\record-window.csproj') -v q -nologo | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'tools\record-window did not build.' }
  $recorder = Get-ChildItem (Join-Path $PSScriptRoot 'record-window\bin') -Recurse -Filter record-window.exe | Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 -ExpandProperty FullName
  New-Item -ItemType Directory -Force $Out | Out-Null
  $capture = Join-Path (Resolve-Path $Out) 'capture.mkv'
  $appArgs = @('--fake-game', $root, '--demo', "`"$DemoQuest`"", '--culture', $Culture, '--window', $(if ($Window) { $Window } else { '1600x900' }))
  $launched = Get-Date
  $p = Start-Process $Exe -ArgumentList $appArgs -PassThru
  # The app's own log says when the demo presses the drawn key ("Demo: press"); the screenshot follows it, as the
  # game's would. Lines from earlier runs in the same log are skipped by their time.
  $logs = Join-Path $env:LOCALAPPDATA 'Shturmap\logs'
  $invariant = [Globalization.CultureInfo]::InvariantCulture # not $culture: that is -Culture
  function DemoTime([string]$what) {
    $file = Get-ChildItem $logs -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $stream = [IO.File]::Open($file.FullName, 'Open', 'Read', 'ReadWrite')
    try { $text = (New-Object IO.StreamReader($stream)).ReadToEnd() } finally { $stream.Dispose() }
    foreach ($line in ($text -split "`r?`n")) {
      if ($line.Length -lt 23 -or -not $line.Contains("Demo: $what")) { continue }
      $at = [datetime]::ParseExact($line.Substring(0, 23), 'yyyy-MM-dd HH:mm:ss.fff', $invariant)
      if ($at -gt $launched) { return $at }
    }
    return $null
  }
  function WaitFor([string]$what, [int]$seconds) {
    $until = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $until) { if (DemoTime $what) { return }; Start-Sleep -Milliseconds 25 }
    throw "The app's log never said 'Demo: $what'."
  }
  try {
    Start-Sleep -Seconds 4
    StartRaid
    Start-Sleep -Seconds 6 # the raid cue is over
    # One short walk on Primorsky Ave toward the taxi extract, about 29 m: the player stands at the first position,
    # and the key press brings the second. These are the only two positions the app gets.
    Shot $walkFrom 0 # "Demo: armed"; the demo starts six seconds later
    Start-Sleep -Seconds 3
    $rec = Start-Process $recorder -ArgumentList '--seconds', $RecordSeconds, '--out', "`"$capture`"", '--ffmpeg', "`"$ffmpegExe`"" -PassThru -NoNewWindow
    $null = $rec.Handle # without it PowerShell loses the exit code
    WaitFor 'press' 15
    Start-Sleep -Milliseconds 170 # the game takes a moment to write the screenshot
    Shot $walkTo 1
    $rec.WaitForExit()
  }
  finally {
    Stop-Process -Id $p.Id -ErrorAction SilentlyContinue
    Remove-Item -Recurse -Force -LiteralPath $root -ErrorAction SilentlyContinue
  }
  if ($rec.ExitCode -ne 0) { throw "tools\record-window failed ($($rec.ExitCode))." }
  # The loop: from just before the key appears to a moment after the view is back. make-media.ps1 crossfades its last
  # 0.6 s into its first frame, so the walk back to the start position never shows as a jump.
  $first = [datetime]::ParseExact((Get-Content ([IO.Path]::ChangeExtension($capture, '.start.txt'))), 'yyyy-MM-dd HH:mm:ss.fff', $invariant)
  foreach ($what in 'key', 'press', 'fix 2', 'end') { if (-not (DemoTime $what)) { throw "The app's log has no 'Demo: $what'." } }
  $in = ((DemoTime 'key') - $first).TotalSeconds - 0.5
  $outAt = ((DemoTime 'end') - $first).TotalSeconds + 0.8
  $lag = ((DemoTime 'fix 2') - (DemoTime 'press')).TotalMilliseconds
  Write-Output ("Key press to position: {0:0} ms" -f $lag)
  if ($in -lt 0 -or $outAt -gt $RecordSeconds) { throw "The demo didn't fit the recording (in $in s, out $outAt s)." }
  $cut = [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:0.000} {1:0.000}', $in, $outAt)
  [IO.File]::WriteAllText((Join-Path (Resolve-Path $Out) 'cut.txt'), $cut)
  Write-Output "Recorded $capture; loop $cut s"
  return
}
$appArgs = @('--fake-game', $root, '--snapshot', $Out, $SnapshotAfter, '--culture', $Culture)
if ($ShowQuest) { $appArgs += @('--show-quest', "`"$ShowQuest`"") }
if ($Window) { $appArgs += @('--window', $Window) }
if ($Scale -gt 1) { $appArgs += @('--snapshot-scale', $Scale) }
$p = Start-Process $Exe -ArgumentList $appArgs -PassThru
if ($PlanOnly) {
  $null = $p.WaitForExit(($SnapshotAfter + 45) * 1000)
  if (-not $p.HasExited) { Stop-Process -Id $p.Id }
  Remove-Item -Recurse -Force -LiteralPath $root -ErrorAction SilentlyContinue
  Write-Output "Snapshots in $Out"
  return
}
Start-Sleep -Seconds 4
StartRaid
Start-Sleep -Seconds 3
Shot $posA
Start-Sleep -Seconds 3
Shot $posB
$null = $p.WaitForExit(($SnapshotAfter + 45) * 1000)
if (-not $p.HasExited) { Stop-Process -Id $p.Id }
Remove-Item -Recurse -Force -LiteralPath $root -ErrorAction SilentlyContinue
Write-Output "Snapshots in $Out"
