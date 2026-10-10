# Plays a scripted raid (Streets, or -Map lab/labyrinth/icebreaker) against Shturmap without the game, and saves
# window/map snapshots mid-raid.
# Usage: .\tools\fake-raid.ps1 -Exe artifacts\Shturmap\Shturmap.exe -Out <folder for PNGs>
# With -Demo it records the website's hero clip instead: the app plays its scripted interaction (src\Shturmap.App\
# Demo.cs), this script writes the one screenshot that follows the demo's key press, and tools\record-window records
# Shturmap's own window into <Out>\capture.mkv; <Out>\cut.txt holds the loop's in and out points in seconds.
param(
  [Parameter(Mandatory)] [string] $Exe,
  [Parameter(Mandatory)] [string] $Out,
  [int] $SnapshotAfter = 16,
  # Part of a quest name: pick and highlight it, and hold its card, before the snapshot (card.png).
  [string] $ShowQuest,
  # The tour's chapter to snapshot (1 to 7; docs/tour.md), at its end; use with -PlanOnly, since the tour waits out raids.
  [int] $Tour,
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
  # The group's leader picks Streets while the app is in the menus (the GROUP PICKED cue and the bring notice).
  [switch] $GroupPick,
  # Stop the raid halfway through loading (the "LOADING · SPAWNING" line in the raid card): no raid start, no fixes.
  [switch] $HoldLoading,
  # Record the hero clip (see above) instead of taking snapshots.
  [switch] $Demo,
  # Part of the name of the quest the demo points at.
  [string] $DemoQuest = 'Road Closed',
  # How long the recorder runs, in seconds; the loop is cut out of it.
  [int] $RecordSeconds = 24,
  # ffmpeg, if it isn't on PATH (winget install --id Gyan.FFmpeg -e).
  [string] $Ffmpeg,
  # The raid's map, for snapshots (the demo always plays Streets). The Lab, Labyrinth and Icebreaker are drawn as
  # sheets (no artwork, docs/DESIGN.md §3).
  [ValidateSet('streets', 'lab', 'labyrinth', 'icebreaker')] [string] $Map = 'streets',
  # More arguments for the app, passed on as they are: --show-report, --show-crash, --whats-new (developer builds),
  # --switch-language <culture> (tools\layout-check.ps1 uses them).
  [string[]] $MoreArgs = @()
)
if ($Demo -and $Map -ne 'streets') { throw 'The demo plays Streets only.' }
$ErrorActionPreference = 'Stop'
$root = Join-Path ([IO.Path]::GetTempPath()) ("shturmap-fake-" + [Guid]::NewGuid().ToString('N').Substring(0, 8))
$start = Get-Date
$session = 'log_' + $start.ToString('yyyy.MM.dd_HH-mm-ss') + '_1.1.5.1.47510'
New-Item -ItemType Directory -Force (Join-Path $root "Logs\$session"), (Join-Path $root 'Screenshots') | Out-Null
$log = Join-Path $root ("Logs\$session\" + $start.ToString('yyyy.MM.dd_HH-mm-ss') + '_1.1.5.1.47510 application_000.log')
# The raid has run for a while when its picture is taken: every log line is written this many minutes in the past, so
# the raid card's time fits the walk the map shows ("13 min left · 27 min in"), where it used to say 0 min beside a
# trail from one spawn to another (owner, 2026-10-05).
$minutesIn = if ($Map -eq 'streets') { 27 } else { 12 }
function Log([string]$message) {
  $line = (Get-Date).AddMinutes(-$minutesIn).ToString('yyyy-MM-dd HH:mm:ss.fff') + "|1.1.5.1.47510|Info|application|$message`r`n"
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
# A screenshot whose picture shows the game's extract list at its top right, as the game shows it at a raid's start:
# a green bar, a row per exit, "??:??:??" beside the ones that may be closed. Drawn here in a Windows typeface (no
# game art), in the game's layout at 2560x1440; Shturmap reads it as it reads a real one (ExitListReader).
function ShotWithList([string]$position, [object[]]$rows, [int]$n = 0) {
  Add-Type -AssemblyName System.Drawing
  $name = (Get-Date).ToString('yyyy-MM-dd[HH-mm]') + "_$position ($n).png"
  $bitmap = New-Object System.Drawing.Bitmap 2560, 1440
  $g = [System.Drawing.Graphics]::FromImage($bitmap)
  try {
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(96, 104, 92))
    $left = 2560 - 768
    $panel = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(14, 16, 14))
    $green = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(124, 167, 14))
    $dark = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(10, 12, 10))
    $light = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(205, 210, 205))
    $big = New-Object System.Drawing.Font 'Bahnschrift', 30, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $small = New-Object System.Drawing.Font 'Bahnschrift', 24, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $g.FillRectangle($panel, $left, 5, 768, 74 + 70 * $rows.Count)
    $g.FillRectangle($green, $left, 5, 573, 74)
    $g.DrawString('Find an extraction point', $big, $dark, $left + 70, 24)
    for ($i = 0; $i -lt $rows.Count; $i++) {
      $y = 5 + 74 + 70 * $i + 20
      $g.DrawString($rows[$i][0], $big, $light, $left + 10, $y)
      $g.DrawString($rows[$i][1], $small, $light, $left + 24 + $g.MeasureString($rows[$i][0], $big).Width, $y + 5)
      $g.DrawString($rows[$i][2], $big, $light, $left + 600, $y)
    }
  }
  finally { $g.Dispose() }
  # Written beside the folder and moved in: the file appears whole, and its name is what the watcher sees.
  $temp = Join-Path $root $name
  $bitmap.Save($temp, [System.Drawing.Imaging.ImageFormat]::Png)
  $bitmap.Dispose()
  Move-Item -LiteralPath $temp (Join-Path $root "Screenshots\$name")
}
# A PMC raid on Streets, as it was walked (positions from a raid's own screenshots' names, each a few minutes after
# the one before): from the spawn at the map's south-east up Primorsky Ave. The first screenshot is taken at the
# raid's start, while the game shows the extract list; the last is the position the pictures show (B).
$streetsList = @(
  @('EXFIL01', 'Courtyard', '??:??:??'), @('EXFIL02', 'Primorsky Ave Taxi V-Ex', '??:??:??'), @('EXFIL03', 'Crash Site', ''),
  @('EXFIL04', 'Damaged House', ''), @('EXFIL05', 'Klimov Street (Flare)', ''), @('EXFIL06', 'Pinewood Basement (Co-Op)', ''),
  @('TRANSIT01', 'Transit to Ground Zero', '0:00:50'), @('TRANSIT02', 'Transit to Interchange', '0:00:50'), @('TRANSIT03', 'Transit to The Lab', '0:00:50'))
$streetsWalk = @(
  '230.68, 1.96, 208.60_-0.03961, 0.59470, -0.02947, -0.80243_17.93',
  '148.55, 3.39, 199.90_-0.03455, -0.94940, 0.11243, -0.29121_18.33',
  '178.94, 4.53, 233.60_0.08900, -0.57194, 0.06117, 0.81315_18.73',
  '274.51, 6.26, 517.28_0.06415, -0.86147, 0.11255, 0.49101_8.23')
$posA = '257.35, -4.43, 51.14_0.02679, -0.27945, 0.00780, 0.95976_17.40'
$posB = '174.81, 4.95, 418.12_0.04140, -0.63128, 0.03257, 0.77376_8.59'
# The demo's walk: south on Primorsky Ave, facing the way it goes.
$walkFrom = '13.02, 3.92, 381.30_0.00500, 0.04500, -0.00050, 0.99900_14.13'
$walkTo = '12.50, 4.00, 410.00_0.00500, 0.04500, -0.00050, 0.99900_14.15'
function GroupNotification([string]$kind, [string]$body) {
  $now = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss.fff')
  [IO.File]::AppendAllText($push, "$now|1.1.5.1.47510|Info|push-notifications|Got notification | $kind`r`n$body`r`n")
}
function PickStreets {
  GroupNotification 'GroupMatchRaidSettings' ("{`r`n  `"type`": `"groupMatchRaidSettings`",`r`n  `"raidSettings`": {`r`n" +
    "    `"location`": `"TarkovStreets`",`r`n    `"timeVariant`": `"CURR`",`r`n    `"raidMode`": `"Online`"`r`n  }`r`n}")
  GroupNotification 'GroupMatchRaidReady' "{`r`n  `"type`": `"groupMatchRaidReady`"`r`n}"
}
# Per map: the scene the log names while loading, the match setup's location, and the two snapshot fixes.
$facing = '0.01000, 0.99900, -0.04000, 0.02000_14.13'
$raidMaps = @{
  streets    = @{ Scene = 'maps/city_preset.bundle rcid:city.scenespreset.asset'; Location = 'TarkovStreets'; A = $posA; B = $posB }
  lab        = @{ Scene = 'maps/laboratory_preset.bundle'; Location = 'laboratory'; A = "-210.00, 0.50, -340.00_$facing"; B = "-200.00, 0.50, -330.00_$facing" }
  labyrinth  = @{ Scene = 'maps/labyrinth_preset.bundle'; Location = 'Labyrinth'; A = "0.00, 0.00, 10.00_$facing"; B = "0.00, 0.00, 20.00_$facing" }
  icebreaker = @{ Scene = 'maps/icebreaker.bundle'; Location = 'Icebreaker'; A = "5.00, 19.20, -10.00_$facing"; B = "5.00, 19.20, 0.00_$facing" }
}
$raidMap = $raidMaps[$Map]
function StartRaid {
  Log "scene preset path:$($raidMap.Scene)"
  $raidProfile = if ($Scav) { '000000000000000000000004' } else { '000000000000000000000003' }
  if (-not $LocalRaid) {
    Log "TRACE-NetworkGameCreate profileStatus: 'Profileid: $raidProfile, Status: Busy, RaidMode: Online, Location: $($raidMap.Location), shortId: FAKE01'"
  }
  if ($HoldLoading) {
    # The steps of a real load (docs/NEXT.md, item 3), up to the spawn; the raid never starts.
    Log 'LocationLoaded:23.5 real:29.87 diff:6.37'
    Log 'GamePrepared:24.1 real:30.6 diff:6.5'
    Log 'GameCreated:24.6(0.5) real:31.1(0.5) diff:6.5'
    Log 'PlayerSpawnEvent:30.3(5.7) real:37.2(6.1) diff:6.9'
    return
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
  # Builds that aren't installed (the folder build, Debug) log to Shturmap-dev, the installed release to Shturmap
  # (docs/DESIGN.md §8, "Data folders"): the newest log file of either is this run's.
  $logs = @('Shturmap-dev\logs', 'Shturmap\logs') | ForEach-Object { Join-Path $env:LOCALAPPDATA $_ } | Where-Object { Test-Path $_ }
  $invariant = [Globalization.CultureInfo]::InvariantCulture # not $culture: that is -Culture
  function DemoTime([string]$what) {
    $file = Get-ChildItem $logs -File -Filter 'shturmap-*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
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
    Start-Sleep -Seconds 9 # the 8 s raid cue is over
    # One short walk on Primorsky Ave toward the taxi extract, about 29 m: the player stands at the first position,
    # and the key press brings the second. These are the only two positions the app gets.
    # The first screenshot shows the extract list, as in the stills: the clip's raid card names an extract on it.
    ShotWithList $walkFrom $streetsList 0 # "Demo: armed"; the demo starts six seconds later
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
  # The loop: from a moment before the pause to a moment after the view is back. make-media.ps1 crossfades its last
  # 0.6 s into its first frame, so the walk back to the start position never shows as a jump.
  $first = [datetime]::ParseExact((Get-Content ([IO.Path]::ChangeExtension($capture, '.start.txt'))), 'yyyy-MM-dd HH:mm:ss.fff', $invariant)
  foreach ($what in 'key', 'press', 'read', 'fix 2', 'end') { if (-not (DemoTime $what)) { throw "The app's log has no 'Demo: $what'." } }
  $in = ((DemoTime 'key') - $first).TotalSeconds - 0.6
  $outAt = ((DemoTime 'end') - $first).TotalSeconds + 0.8
  Write-Output ("Key press to position read: {0:0} ms; shown as the pause ends, {1:0} ms after the press" -f `
    ((DemoTime 'read') - (DemoTime 'press')).TotalMilliseconds, ((DemoTime 'fix 2') - (DemoTime 'press')).TotalMilliseconds)
  if ($in -lt 0 -or $outAt -gt $RecordSeconds) { throw "The demo didn't fit the recording (in $in s, out $outAt s)." }
  $cut = [string]::Format([Globalization.CultureInfo]::InvariantCulture, '{0:0.000} {1:0.000}', $in, $outAt)
  [IO.File]::WriteAllText((Join-Path (Resolve-Path $Out) 'cut.txt'), $cut)
  Write-Output "Recorded $capture; loop $cut s"
  return
}
$appArgs = @('--fake-game', $root, '--snapshot', $Out, $SnapshotAfter, '--culture', $Culture)
if ($ShowQuest) { $appArgs += @('--show-quest', "`"$ShowQuest`"") }
if ($Tour -gt 0) { $appArgs += @('--tour', $Tour) }
if ($Window) { $appArgs += @('--window', $Window) }
if ($Scale -gt 1) { $appArgs += @('--snapshot-scale', $Scale) }
$appArgs += $MoreArgs
$p = Start-Process $Exe -ArgumentList $appArgs -PassThru
if ($GroupPick) {
  # Live, after the startup replay and the game data, as in the menus between raids.
  Start-Sleep -Seconds 6
  PickStreets
}
if ($PlanOnly) {
  $null = $p.WaitForExit(($SnapshotAfter + 45) * 1000)
  if (-not $p.HasExited) { Stop-Process -Id $p.Id }
  Remove-Item -Recurse -Force -LiteralPath $root -ErrorAction SilentlyContinue
  Write-Output "Snapshots in $Out"
  return
}
Start-Sleep -Seconds 4
StartRaid
if (-not $HoldLoading) {
  Start-Sleep -Seconds 3
  if ($Map -eq 'streets' -and -not $Scav) {
    # The raid's first screenshot shows the extract list; then the walk, and the position the picture is taken at.
    ShotWithList $raidMap.A $streetsList
    Start-Sleep -Milliseconds 1500
    foreach ($at in $streetsWalk) { Shot $at; Start-Sleep -Milliseconds 400 }
    Shot $raidMap.B
  }
  else {
    Shot $raidMap.A
    Start-Sleep -Seconds 3
    Shot $raidMap.B
  }
}
$null = $p.WaitForExit(($SnapshotAfter + 45) * 1000)
if (-not $p.HasExited) { Stop-Process -Id $p.Id }
Remove-Item -Recurse -Force -LiteralPath $root -ErrorAction SilentlyContinue
Write-Output "Snapshots in $Out"
