# Copies the application and push-notifications logs of chosen EFT log sessions into tests/fixtures/logs,
# scrubbed of account-identifying values. Never copies backend logs (they hold a session token).
# Profile ids are replaced by stable placeholders (000…001, 000…002, …) so tests can still tell the PMC
# profile from the Scav profile.
# Usage: .\tools\make-log-fixtures.ps1 -LogsRoot 'C:\...\build\Logs' -Sessions log_2026.01.01_15-00-00_1.1.5.1.47510
param(
  [Parameter(Mandatory)] [string] $LogsRoot,
  [Parameter(Mandatory)] [string[]] $Sessions,
  [string] $Destination = (Join-Path $PSScriptRoot '..\tests\fixtures\logs')
)
$ErrorActionPreference = 'Stop'
$profiles = @{}
$pseudonym = {
  param($m)
  $id = $m.Groups['id'].Value.ToLowerInvariant()
  if (-not $profiles.ContainsKey($id)) { $profiles[$id] = ($profiles.Count + 1).ToString().PadLeft(24, '0') }
  $m.Groups['pre'].Value + $profiles[$id]
}
foreach ($session in $Sessions) {
  $source = Join-Path $LogsRoot $session
  $target = Join-Path $Destination $session
  New-Item -ItemType Directory -Force $target | Out-Null
  Get-ChildItem $source -File | Where-Object { $_.Name -match ' (application|push-notifications)_\d+\.log$' } | ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName)
    $text = [regex]::Replace($text, '(?i)(?<pre>profileid:\s*)(?<id>[0-9a-f]{24})', $pseudonym)
    $text = [regex]::Replace($text, '(?i)(?<pre>"profileid"\s*:\s*")(?<id>[0-9a-f]{24})', $pseudonym)
    $text = $text -replace 'AccountId:\d+', 'AccountId:0'
    $text = $text -replace '(?i)"(\w*token\w*|aid|accountId|session\w*|sid)"\s*:\s*"[^"]*"', '"$1": "redacted"'
    $text = $text -replace '(?i)\bSid: [^,'']+', 'Sid: redacted'
    $text = $text -replace '\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}(:\d+)?\b(?!\.\d)', '0.0.0.0'
    [IO.File]::WriteAllText((Join-Path $target $_.Name), $text, [Text.UTF8Encoding]::new($false))
  }
  Write-Output "fixture: $session"
}
