# Copies the application and push-notifications logs of chosen EFT log sessions into tests/fixtures/logs,
# scrubbed of everything that identifies a player. Never copies backend logs (they hold a session token).
# The copies stay on this PC (git ignores tests/fixtures/logs; no game log goes into the repository, owner 2026-10-04),
# and they are scrubbed all the same, should one ever be passed on: an id stays only where it is public game data
# (a quest or trader id in a message's templateId, an item template in "_tpl", a trader as a message's dialogId or
# uid). Every other id
# becomes a placeholder: profile ids 000…001, 000…002, … (stable, so tests can still tell the PMC profile from the
# Scav profile, also on the "[Transit] `<id>`" line that names the profile playing), any other id (messages, events,
# item instances, the stash, raids) ffffffff000…001, …, and the push channel's id zeros. Tokens, session ids, account
# ids and network addresses are masked. tests\Shturmap.Core.Tests\FixtureScrubTests.cs fails if a fixture breaks this.
# Each copy also gets a made-up name and time (-As, one per session, the name the tests use): every time in it (the
# lines' times, a message's "dt", an insurer's date and time) and its files' names move by the same amount, so the
# session starts when its new name says. A session's real start, with the quests done in it, would tell the player
# apart to the game's servers.
# Usage: .\tools\make-log-fixtures.ps1 -LogsRoot 'C:\...\build\Logs' -Sessions log_<date>_<time>_<build>
#          -As log_2026.01.01_15-00-00_1.1.5.1.47510
param(
  [Parameter(Mandatory)] [string] $LogsRoot,
  [Parameter(Mandatory)] [string[]] $Sessions,
  [Parameter(Mandatory)] [string[]] $As,
  [string] $Destination = (Join-Path $PSScriptRoot '..\tests\fixtures\logs')
)
$ErrorActionPreference = 'Stop'
if ($As.Count -ne $Sessions.Count) { throw '-As needs one made-up name for each session.' }
$inv = [Globalization.CultureInfo]::InvariantCulture
# "log_2026.01.01_15-00-00_1.1.5.1.47510" → its start and its build.
function SessionStart([string]$name) {
  if ($name -notmatch '^log_(?<d>\d{4}\.\d{2}\.\d{2})_(?<t>\d{1,2}-\d{2}-\d{2})_(?<build>.+)$') {
    throw "Not a log session folder's name: $name"
  }
  [pscustomobject]@{ Start = [datetime]::ParseExact("$($Matches.d) $($Matches.t)", 'yyyy.MM.dd H-mm-ss', $inv); Build = $Matches.build }
}
# tarkov.dev's trader ids: public, and the only ids a message's dialogId and uid keep (anything else is a player).
$traders = @(
  '54cb50c76803fa8b248b4571', '54cb57776803fa99248b456e', '579dc571d53a0658a154fbec', '58330581ace78e27b8b10cee',
  '5935c25fb3acc3127c3d8cd9', '5a7c2eca46aef81a7ca2145d', '5ac3b934156ae10c4430e83c', '5c0647fdd443bc2504c2d371',
  '638f541a29ffd1183d187f57', '656f0f98d80a697f855d34b1', '6617beeaa9cfa777ca915b7c')
$profiles = @{}
$pseudonym = {
  param($m)
  $id = $m.Groups['id'].Value.ToLowerInvariant()
  if (-not $profiles.ContainsKey($id)) { $profiles[$id] = ($profiles.Count + 1).ToString().PadLeft(24, '0') }
  $m.Groups['pre'].Value + $profiles[$id]
}
$others = @{}
$script:current = ''
# Any id left after the profiles: kept only where it is public game data, told by what stands before it on its line.
$other = {
  param($m)
  $id = $m.Value.ToLowerInvariant()
  if ($id -match '^(0{16}\d{8}|f{8}\d{16}|0{25,})$') { return $m.Value }
  $from = [Math]::Max(0, $m.Index - 400)
  $before = $script:current.Substring($from, $m.Index - $from)
  $before = $before.Substring($before.LastIndexOf("`n") + 1)
  if ($before -match '"_tpl"\s*:\s*"$' -or $before -match '"templateId"\s*:\s*"[^"]*$') { return $m.Value }
  if ($before -match '"(dialogId|uid)"\s*:\s*"$' -and $traders -contains $id) { return $m.Value }
  if (-not $others.ContainsKey($id)) { $others[$id] = 'ffffffff' + ($others.Count + 1).ToString().PadLeft(16, '0') }
  $others[$id]
}
for ($i = 0; $i -lt $Sessions.Count; $i++) {
  $session = $Sessions[$i]
  $alias = $As[$i]
  $real = SessionStart $session
  $made = SessionStart $alias
  if ($made.Build -ne $real.Build) { throw "$alias names another build than the session's ($($real.Build))." }
  $script:shift = $made.Start - $real.Start
  $source = Join-Path $LogsRoot $session
  $target = Join-Path $Destination $alias
  New-Item -ItemType Directory -Force $target | Out-Null
  Get-ChildItem $source -File | Where-Object { $_.Name -match ' (application|push-notifications)_\d+\.log$' } | ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName)
    # The times: a line's local time, a message's "dt" (Unix seconds, the server's clock, read as local time), and the
    # insurer's date and time in a message's systemData.
    $text = [regex]::Replace($text, '(?m)^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?=\.\d{3})', {
        param($m) [datetime]::ParseExact($m.Value, 'yyyy-MM-dd HH:mm:ss', $inv).Add($script:shift).ToString('yyyy-MM-dd HH:mm:ss', $inv) })
    $text = [regex]::Replace($text, '(?<pre>"dt"\s*:\s*)(?<s>\d{9,11})(?![\d])', {
        param($m)
        $local = [DateTimeOffset]::FromUnixTimeSeconds([long]$m.Groups['s'].Value).LocalDateTime.Add($script:shift)
        $m.Groups['pre'].Value + [DateTimeOffset]::new($local).ToUnixTimeSeconds() })
    $text = [regex]::Replace($text, '(?<a>"date"\s*:\s*")(?<d>\d{2}\.\d{2}\.\d{4})(?<b>"\s*,\s*"time"\s*:\s*")(?<t>\d{2}:\d{2})"', {
        param($m)
        $at = [datetime]::ParseExact("$($m.Groups['d'].Value) $($m.Groups['t'].Value)", 'dd.MM.yyyy HH:mm', $inv).Add($script:shift)
        $m.Groups['a'].Value + $at.ToString('dd.MM.yyyy', $inv) + $m.Groups['b'].Value + $at.ToString('HH:mm', $inv) + '"' })
    $text = [regex]::Replace($text, '(?i)(?<pre>profileid:\s*)(?<id>[0-9a-f]{24})', $pseudonym)
    $text = [regex]::Replace($text, '(?i)(?<pre>"profileid"\s*:\s*")(?<id>[0-9a-f]{24})', $pseudonym)
    $text = [regex]::Replace($text, '(?i)(?<pre>\[Transit\] `)(?<id>[0-9a-f]{24})', $pseudonym)
    $text = [regex]::Replace($text, '(?i)(?<pre>getwebsocket/)(?<id>[0-9a-f]{16,})', { param($m) $m.Groups['pre'].Value + ('0' * $m.Groups['id'].Length) })
    $script:current = $text
    $text = [regex]::Replace($text, '(?i)\b[0-9a-f]{24,}\b', $other)
    $text = $text -replace 'AccountId:\d+', 'AccountId:0'
    $text = $text -replace '(?i)"(\w*token\w*|aid|accountId|session\w*|sid)"\s*:\s*"[^"]*"', '"$1": "redacted"'
    $text = $text -replace '(?i)\bSid: [^,'']+', 'Sid: redacted'
    $text = $text -replace '\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d+)?\b(?!\.\d)', '0.0.0.0'
    # "<session without log_> application_000.log", named as the made-up session.
    $name = $_.Name.Replace($session.Substring(4), $alias.Substring(4))
    [IO.File]::WriteAllText((Join-Path $target $name), $text, [Text.UTF8Encoding]::new($false))
  }
  Write-Output "fixture: $alias"
}
