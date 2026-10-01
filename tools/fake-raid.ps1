# Plays a scripted Streets raid against Shturmap without the game, and saves window/map snapshots mid-raid.
# Usage: .\tools\fake-raid.ps1 -Exe artifacts\Shturmap\Shturmap.exe -Out <folder for PNGs>
param(
  [Parameter(Mandatory)] [string] $Exe,
  [Parameter(Mandatory)] [string] $Out,
  [int] $SnapshotAfter = 16,
  # Part of a quest name: highlight it, hold its card and pin it before the snapshot.
  [string] $ShowQuest,
  # Play the raid as a Scav: the match setup names another profile than the menu's.
  [switch] $Scav
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
function Shot([string]$position) {
  $name = (Get-Date).ToString('yyyy-MM-dd[HH-mm]') + "_$position (0).png"
  [IO.File]::WriteAllText((Join-Path $root "Screenshots\$name"), '')
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
$appArgs = @('--fake-game', $root, '--snapshot', $Out, $SnapshotAfter)
if ($ShowQuest) { $appArgs += @('--show-quest', "`"$ShowQuest`"") }
$p = Start-Process $Exe -ArgumentList $appArgs -PassThru
Start-Sleep -Seconds 4
Log 'scene preset path:maps/city_preset.bundle rcid:city.scenespreset.asset'
$raidProfile = if ($Scav) { '000000000000000000000004' } else { '000000000000000000000003' }
Log "TRACE-NetworkGameCreate profileStatus: 'Profileid: $raidProfile, Status: Busy, RaidMode: Online, Location: TarkovStreets, shortId: FAKE01'"
Log 'GameStarting:80.26(1.7) real:95.46(2.73) diff:15.19'
Start-Sleep -Seconds 1
Log 'GameStarted:90.6(10.33) real:107.49(12.02) diff:16.89'
Start-Sleep -Seconds 3
Shot '-60.00, 3.50, 300.00_-0.02500, 0.23500, -0.00500, -0.97150_6.45'
Start-Sleep -Seconds 3
Shot '40.00, 2.50, 120.00_0.01000, 0.99900, -0.04000, 0.02000_14.13'
$null = $p.WaitForExit(($SnapshotAfter + 45) * 1000)
if (-not $p.HasExited) { Stop-Process -Id $p.Id }
Remove-Item -Recurse -Force -LiteralPath $root -ErrorAction SilentlyContinue
Write-Output "Snapshots in $Out"
