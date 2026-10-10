# The layout check (docs/LANGUAGES.md, "Layout check"): every view of Shturmap in each language and at each window
# size, and what a longer language breaks in it. It plays each view with tools\fake-raid.ps1 against a developer build,
# whose snapshot writes beside each picture its texts (<picture>.texts.txt), its problems (<picture>.layout.json) and
# the picture with them boxed (<picture>.problems.png; src\Shturmap.App\Dev\LayoutCheck.cs), and sums them up in
# <Out>\summary.md.
# Usage: .\tools\layout-check.ps1 -Out <folder> [-Exe <a developer build's Shturmap.exe>] [-Languages en-US,de-DE,qps-ploc]
#        [-Sizes 900x560,1600x900] [-States plan,raid,...] [-SwitchCheck] [-SummaryOnly]
# Exit code: 0 when every language but the pseudo-language is free of problems (the pseudo-language's are warnings:
# they forecast what a longer language will break); 1 when one has a problem, or a switch differs from a fresh start;
# 2 when a view couldn't be checked.
param(
  [Parameter(Mandatory)] [string] $Out,
  # A developer build's Shturmap.exe (Debug, or built with ShturmapDev=true): the check is compiled into those only.
  # Empty: built into artifacts\layout-check\app.
  [string] $Exe,
  # Cultures, as --culture takes them; qps-ploc is the pseudo-language.
  [string[]] $Languages = @('en-US', 'de-DE', 'qps-ploc'),
  # Window sizes in DIP, as Windows counts a window (title bar and frame included): 900x560 is the smallest Shturmap
  # allows (docs/DESIGN.md §4, "The window"). Scaled to the pixels of the monitor the app opens on.
  [string[]] $Sizes = @('900x560', '1600x900'),
  # Which views (all when empty): plan, raid, report, crash, whatsnew, quest, tour1 … tour7. Settings (settings.png) is
  # in every one, as each snapshot opens it; help (help.png) in those where it opens by itself, all but the tour's.
  [string[]] $States,
  # Also start in English, switch to each other language once the data is there (--switch-language), and compare every
  # text on screen with a fresh start in that language.
  [switch] $SwitchCheck,
  # Seconds from the start to the snapshot (tools\fake-raid.ps1 -SnapshotAfter).
  [int] $SnapshotAfter = 16,
  # Only sum up again what an earlier run left in -Out (after a fix to this script, or with other -Languages).
  [switch] $SummaryOnly
)
$ErrorActionPreference = 'Stop'
# Typographic characters by their codes: Windows PowerShell 5.1 reads a script without a BOM in the ANSI code page.
$dot = [string][char]0x00B7; $ellipsis = [string][char]0x2026; $enDash = [string][char]0x2013; $times = [string][char]0x00D7
$repo = Split-Path $PSScriptRoot -Parent
$pseudo = 'qps-ploc'

# The views: each a run of tools\fake-raid.ps1.
$views = [ordered]@{
  plan     = @{ PlanOnly = $true }
  raid     = @{ PlanOnly = $false }
  report   = @{ PlanOnly = $true; AppArgs = @('--show-report') }
  crash    = @{ PlanOnly = $true; AppArgs = @('--show-crash') }
  whatsnew = @{ PlanOnly = $true; AppArgs = @('--whats-new') }
  # A quest's card and its first need's item card (card.png, card-2.png).
  quest    = @{ PlanOnly = $true; ShowQuest = 'Dandies' }
}
foreach ($n in 1..7) { $views["tour$n"] = @{ PlanOnly = $true; Tour = $n } }
if ($States) {
  $unknown = @($States | Where-Object { -not $views.Contains($_) })
  if ($unknown) { throw "Unknown views: $($unknown -join ', '). Known: $($views.Keys -join ', ')." }
}
$chosen = @(if ($States) { $views.Keys | Where-Object { $States -contains $_ } } else { $views.Keys })

New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$switchTo = @(if ($SwitchCheck) { $Languages | Where-Object { $_ -notlike 'en*' } })

if (-not $SummaryOnly) {
  # The developer build.
  if (-not $Exe) {
    $app = Join-Path $repo 'artifacts\layout-check\app'
    $Exe = Join-Path $app 'Shturmap.exe'
    $running = Get-CimInstance Win32_Process -Filter "Name = 'Shturmap.exe'" | Where-Object { $_.ExecutablePath -eq $Exe }
    if ($running) { throw "Shturmap is running from $app (process $($running.ProcessId -join ', ')). Close it and run again." }
    if (Test-Path $app) { Remove-Item $app -Recurse -Force }
    & (Join-Path $repo 'eng\dotnet.ps1') publish (Join-Path $repo 'src\Shturmap.App\Shturmap.App.csproj') -c Release -o $app '-p:ShturmapDev=true'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
  }
  $Exe = (Resolve-Path $Exe).Path
  $dll = Join-Path (Split-Path $Exe) 'Shturmap.dll'
  if ((Test-Path $dll) -and -not ([Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll)).Contains('LayoutCheck'))) {
    throw "$Exe is no developer build: the layout check is compiled into Debug and ShturmapDev=true builds only. Leave out -Exe to build one."
  }

  # The monitor the app opens on (the first that isn't the primary one, else the primary: MainWindow.PlaceOnSecondMonitor)
  # and its scale, so a size in DIP is the window's size in pixels there.
  Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public static class LayoutCheckMonitors
{
    private delegate bool MonitorProc(IntPtr monitor, IntPtr hdc, IntPtr rect, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorProc proc, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref Info info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [StructLayout(LayoutKind.Sequential)] private struct Box { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Info { public int Size; public Box Monitor; public Box Work; public uint Flags; }
    // Each monitor's scale (1 at 100 %), the primary one marked by a negative sign.
    public static List<double> Scales()
    {
        var previous = SetThreadDpiAwarenessContext(new IntPtr(-4)); // per-monitor aware: the real DPI
        var scales = new List<double>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, hdc, rect, data) =>
        {
            var info = new Info { Size = Marshal.SizeOf(typeof(Info)) };
            GetMonitorInfo(monitor, ref info);
            uint x, y;
            var scale = GetDpiForMonitor(monitor, 0, out x, out y) == 0 && x > 0 ? x / 96.0 : 1.0;
            scales.Add((info.Flags & 1) == 1 ? -scale : scale);
            return true;
        }, IntPtr.Zero);
        SetThreadDpiAwarenessContext(previous);
        return scales;
    }
}
'@
  $scales = [LayoutCheckMonitors]::Scales()
  $others = @($scales | Where-Object { $_ -gt 0 })
  $scale = if ($others) { $others[0] } else { -($scales | Select-Object -First 1) }
  if (@($others | Select-Object -Unique).Count -gt 1) {
    Write-Warning "The monitors other than the primary have different scales; sizes are counted at $([int]($scale * 100)) %. Check each view's window size in the summary."
  }
  $pixelSizes = [ordered]@{}
  foreach ($size in $Sizes) {
    if ($size -notmatch '^(\d+)x(\d+)$') { throw "A size is WIDTHxHEIGHT in DIP, e.g. 900x560: $size" }
    $pixelSizes[$size] = '{0}x{1}' -f [int][Math]::Round([int]$Matches[1] * $scale), [int][Math]::Round([int]$Matches[2] * $scale)
  }

  $fakeRaid = Join-Path $PSScriptRoot 'fake-raid.ps1'
  function Run-View([string]$culture, [string]$size, [string]$view, [string]$folder, [string[]]$more) {
    if (Test-Path $folder) { Remove-Item $folder -Recurse -Force }
    New-Item -ItemType Directory -Force $folder | Out-Null
    $spec = $views[$view]
    $arguments = @{ Exe = $Exe; Out = $folder; Culture = $culture; Window = $pixelSizes[$size]; SnapshotAfter = $SnapshotAfter }
    if ($spec.PlanOnly) { $arguments.PlanOnly = $true }
    if ($spec.Tour) { $arguments.Tour = $spec.Tour }
    if ($spec.ShowQuest) { $arguments.ShowQuest = $spec.ShowQuest }
    $appArgs = @($spec.AppArgs) + @($more) | Where-Object { $_ }
    if ($appArgs) { $arguments.MoreArgs = $appArgs }
    & $fakeRaid @arguments | Out-Null
  }

  $total = $Languages.Count * $Sizes.Count * $chosen.Count + $switchTo.Count * $Sizes.Count * $chosen.Count
  Write-Output ("{0} runs of about {1} s each: {2} language(s) x {3} size(s) x {4} view(s){5}; window sizes at {6} %." -f $total, ($SnapshotAfter + 4),
    $Languages.Count, $Sizes.Count, $chosen.Count, $(if ($switchTo) { ", and the switch to $($switchTo -join ', ')" } else { '' }), [int]($scale * 100))
  $done = 0
  foreach ($culture in $Languages) {
    foreach ($size in $Sizes) {
      foreach ($view in $chosen) {
        $done++
        Write-Output "[$done/$total] $culture $size $view"
        Run-View $culture $size $view (Join-Path $Out "$culture\$size\$view") @()
      }
    }
  }
  foreach ($culture in $switchTo) {
    foreach ($size in $Sizes) {
      foreach ($view in $chosen) {
        $done++
        Write-Output "[$done/$total] en-US switched to $culture, $size $view"
        Run-View 'en-US' $size $view (Join-Path $Out "switch\$culture\$size\$view") @('--switch-language', $culture)
      }
    }
  }
}

# ---- the summary ----

$userFolder = [Environment]::GetFolderPath('UserProfile')
function Masked([string]$text) { if ($userFolder.Length -gt 3) { $text.Replace($userFolder, '%USERPROFILE%') } else { $text } }
function Cell([string]$text) { (Masked $text).Replace("`n", ' ').Replace('|', '\|') }
function Short([string]$text, [int]$length = 90) { $one = (Cell $text); if ($one.Length -le $length) { $one } else { $one.Substring(0, $length - 1) + $ellipsis } }
$kinds = @('overflow', 'trimmed', 'overlap', 'untranslated')
$lines = New-Object System.Collections.Generic.List[string]
$failed = New-Object System.Collections.Generic.List[string]
$unchecked = New-Object System.Collections.Generic.List[string]
$warnings = 0
$rows = New-Object System.Collections.Generic.List[string]
$sections = New-Object System.Collections.Generic.List[string]

foreach ($culture in $Languages) {
  $isPseudo = $culture -eq $pseudo
  # Untranslated texts are one to-do list for the language, whatever the size: each text once, with where it shows.
  $untranslated = [ordered]@{}
  foreach ($size in $Sizes) {
    $groups = [ordered]@{}
    $sizeUntranslated = @{}
    $windowNote = $null
    foreach ($view in $chosen) {
      $folder = Join-Path $Out "$culture\$size\$view"
      $files = @(Get-ChildItem $folder -Filter '*.layout.json' -ErrorAction SilentlyContinue)
      if (-not $files -or -not (Test-Path (Join-Path $folder 'window.layout.json'))) {
        $unchecked.Add("$culture $size $view") | Out-Null
        continue
      }
      foreach ($file in $files) {
        $result = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $picture = $result.picture
        if ($picture -eq 'window.png' -and -not $windowNote) {
          $w = $result.window
          $windowNote = "{0}$times{1} DIP outside ({2}$times{3} px at {4} %), {5}$times{6} DIP inside" -f [Math]::Round($w.outerWidth / $w.scale), [Math]::Round($w.outerHeight / $w.scale),
            $w.outerWidth, $w.outerHeight, [Math]::Round($w.scale * 100), $w.width, $w.height
        }
        foreach ($p in $result.problems) {
          # Figures change from run to run (a time, a fix's age): problems are told apart without them.
          if ($p.kind -eq 'untranslated') {
            $key = ($p.detail -replace '\d+', '#')
            $sizeUntranslated[$key] = $true
            if (-not $untranslated.Contains($key)) {
              $untranslated[$key] = @{ Problem = $p; Pictures = New-Object System.Collections.Generic.List[string]; Views = New-Object System.Collections.Generic.List[string] }
            }
            if (-not $untranslated[$key].Pictures.Contains($picture)) { $untranslated[$key].Pictures.Add($picture) | Out-Null }
            if (-not $untranslated[$key].Views.Contains($view)) { $untranslated[$key].Views.Add($view) | Out-Null }
            continue
          }
          # The same problem in several views (help is in every one) is listed once, with where it shows.
          $key = "$($p.kind)|$picture|$($p.element)|$($p.text -replace '\d+', '#')|$($p.detail -replace '\d+', '#')"
          if (-not $groups.Contains($key)) { $groups[$key] = @{ Problem = $p; Picture = $picture; Where = New-Object System.Collections.Generic.List[string] } }
          $stem = [IO.Path]::GetFileNameWithoutExtension($picture)
          $link = ("$culture/$size/$view/$stem.problems.png").Replace(' ', '%20')
          $groups[$key].Where.Add("[$view #$($p.n)]($link)") | Out-Null
        }
      }
    }
    $counts = @{}
    foreach ($k in $kinds) { $counts[$k] = @($groups.Values | Where-Object { $_.Problem.kind -eq $k }).Count }
    $counts.untranslated = $sizeUntranslated.Count
    $problemCount = ($kinds | ForEach-Object { $counts[$_] } | Measure-Object -Sum).Sum
    if ($problemCount -gt 0) { if ($isPseudo) { $warnings += $problemCount } else { $failed.Add("$culture $size") | Out-Null } }
    $rows.Add(('| {0} | {1} | {2} | {3} | {4} | {5} |' -f $culture, $size, $counts.overflow, $counts.trimmed, $counts.overlap,
        $(if ($isPseudo) { $counts.untranslated } else { $enDash }))) | Out-Null
    $sections.Add('') | Out-Null
    $sections.Add("## $culture $dot $size$(if ($isPseudo) { ' (pseudo-language: warnings)' })") | Out-Null
    $sections.Add('') | Out-Null
    if ($windowNote) { $sections.Add("Window: $windowNote.") | Out-Null; $sections.Add('') | Out-Null }
    if ($groups.Count -eq 0) { $sections.Add("No overflow, trimmed or overlapping texts.") | Out-Null; $sections.Add('') | Out-Null }
    foreach ($kind in $kinds) {
      $ofKind = @($groups.Values | Where-Object { $_.Problem.kind -eq $kind })
      if (-not $ofKind) { continue }
      $sections.Add("### $kind ($($ofKind.Count))") | Out-Null
      $sections.Add('') | Out-Null
      foreach ($g in $ofKind) {
        $p = $g.Problem
        $hidden = if ($p.inView) { '' } else { ' (not in view)' }
        $sections.Add(('- {0}: "{1}" in {2}: {3}{4}. {5}' -f $g.Picture, (Short $p.text), (Cell $p.element), (Cell $p.detail), $hidden,
            ($g.Where -join ', '))) | Out-Null
      }
      $sections.Add('') | Out-Null
    }
  }
  if ($untranslated.Count -gt 0) {
    $sections.Add('') | Out-Null
    $sections.Add("## $culture $dot untranslated ($($untranslated.Count) texts, every size)") | Out-Null
    $sections.Add('') | Out-Null
    $sections.Add("Each text once, with what of it isn't in the pseudo-language, the pictures and the views it shows in (a tooltip or a screen reader's name says so).") | Out-Null
    $sections.Add('') | Out-Null
    foreach ($u in $untranslated.Values) {
      $p = $u.Problem
      $views = if ($u.Views.Count -eq $chosen.Count) { 'every view' } elseif ($u.Views.Count -gt 4) { "$($u.Views.Count) views" } else { $u.Views -join ', ' }
      $sections.Add(('- "{0}": {1}; {2}; {3}' -f (Short $p.text 120), (Cell $p.detail), ($u.Pictures -join ', '), $views)) | Out-Null
    }
  }
}

# Switching while running: every text of a view after the switch must be what a fresh start in that language shows.
$switchRows = New-Object System.Collections.Generic.List[string]
$switchChecked = 0
foreach ($culture in $switchTo) {
  foreach ($size in $Sizes) {
    foreach ($view in $chosen) {
      $switched = Join-Path $Out "switch\$culture\$size\$view"
      $fresh = Join-Path $Out "$culture\$size\$view"
      if (-not (Test-Path (Join-Path $switched 'window.layout.json')) -or -not (Test-Path (Join-Path $fresh 'window.layout.json'))) {
        $unchecked.Add("switch to $culture $size $view") | Out-Null
        continue
      }
      $a = Get-Content (Join-Path $fresh 'window.layout.json') -Raw -Encoding UTF8 | ConvertFrom-Json
      $b = Get-Content (Join-Path $switched 'window.layout.json') -Raw -Encoding UTF8 | ConvertFrom-Json
      if ($a.language -ne $b.language -or $a.culture -ne $b.culture) {
        $unchecked.Add("switch to $culture $size ${view}: the app didn't switch (it shows $($b.language), $($b.culture); a fresh start $($a.language), $($a.culture)); this build has no --switch-language?") | Out-Null
        continue
      }
      $switchChecked++
      foreach ($texts in Get-ChildItem $fresh -Filter '*.texts.txt') {
        $other = Join-Path $switched $texts.Name
        # Figures change from one run to the next (times, minutes, a fix's age), and so does the fake game's folder
        # (tools\fake-raid.ps1 makes a new one each run, shown in settings and the lights' tooltips): compared without them.
        # What a report sends ("english: ", the Report dialog's preview) is English and holds the run's own log: left out.
        $before = @(if (Test-Path $other) { Get-Content $other -Encoding UTF8 | Where-Object { -not $_.StartsWith('english: ') } | ForEach-Object { $_ -replace 'shturmap-fake-[0-9a-f]+', 'shturmap-fake-#' -replace '\d+', '#' } })
        $after = @(Get-Content $texts.FullName -Encoding UTF8 | Where-Object { -not $_.StartsWith('english: ') } | ForEach-Object { $_ -replace 'shturmap-fake-[0-9a-f]+', 'shturmap-fake-#' -replace '\d+', '#' })
        # Lines as a multiset, case and order kept apart: what one side has more often than the other.
        $tally = New-Object 'System.Collections.Generic.Dictionary[string,int]'
        foreach ($l in $after) { $n = 0; [void]$tally.TryGetValue($l, [ref]$n); $tally[$l] = $n + 1 }
        foreach ($l in $before) { $n = 0; [void]$tally.TryGetValue($l, [ref]$n); $tally[$l] = $n - 1 }
        $diff = @($tally.GetEnumerator() | Where-Object { $_.Value -ne 0 })
        if ($diff.Count -eq 0) { continue }
        $failed.Add("switch to $culture $size") | Out-Null
        foreach ($d in $diff | Select-Object -First 20) {
          $where = if ($d.Value -lt 0) { 'only after the switch (kept from before)' } else { 'only in a fresh start (missing after the switch)' }
          $switchRows.Add(("- {0} $dot {1} $dot {2} $dot {3}: `"{4}`" {5}" -f $culture, $size, $view, $texts.Name, (Short $d.Key), $where)) | Out-Null
        }
        if ($diff.Count -gt 20) { $switchRows.Add("- $ellipsis $($diff.Count - 20) more in $culture $dot $size $dot $view $dot $($texts.Name)") | Out-Null }
      }
    }
  }
}

$result = if ($unchecked.Count -gt 0) { 'NOT ALL CHECKED' } elseif ($failed.Count -gt 0) { 'PROBLEMS' } else { 'PASSED' }
$lines.Add('# Layout check') | Out-Null
$lines.Add('') | Out-Null
$lines.Add("$(Get-Date -Format 'yyyy-MM-dd HH:mm') $dot $(if ($Exe) { Masked $Exe } else { 'an earlier run' }) $dot views: $($chosen -join ', ')") | Out-Null
$lines.Add('') | Out-Null
$lines.Add("**$result.** " + $(if ($failed.Count) { "Problems in: $(@($failed | Select-Object -Unique) -join '; '). " } else { '' }) +
  $(if ($warnings) { "Pseudo-language: $warnings warnings (what a longer language will break, and texts not in the texts files yet). " } else { '' })) | Out-Null
$lines.Add('') | Out-Null
$lines.Add("Problems per language and size, each counted once however many views show it. Kinds: overflow (sticks out of what clips it or of the window, cut off without `"$ellipsis`", a word that breaks inside), trimmed (`"$ellipsis`" where the design doesn't let a text give way), overlap (text over text), untranslated (pseudo-language only: not in the texts files).") | Out-Null
$lines.Add('') | Out-Null
$lines.Add('| language | size (DIP) | overflow | trimmed | overlap | untranslated |') | Out-Null
$lines.Add('| --- | --- | --- | --- | --- | --- |') | Out-Null
foreach ($row in $rows) { $lines.Add($row) | Out-Null }
if ($unchecked.Count -gt 0) {
  $lines.Add('') | Out-Null
  $lines.Add('## Not checked') | Out-Null
  $lines.Add('') | Out-Null
  foreach ($u in $unchecked) { $lines.Add("- $u") | Out-Null }
}
if ($switchTo) {
  $lines.Add('') | Out-Null
  $lines.Add('## Switching while running') | Out-Null
  $lines.Add('') | Out-Null
  if ($switchChecked -eq 0) { $lines.Add('No view could be compared: see Not checked.') | Out-Null }
  elseif ($switchRows.Count -eq 0) { $lines.Add("In the $switchChecked views compared, every text after the switch is what a fresh start shows.") | Out-Null }
  foreach ($row in $switchRows) { $lines.Add($row) | Out-Null }
}
foreach ($line in $sections) { $lines.Add($line) | Out-Null }
$summary = Join-Path $Out 'summary.md'
[IO.File]::WriteAllText($summary, (($lines -join "`r`n") + "`r`n"), (New-Object Text.UTF8Encoding($false)))
Write-Output "$result. Summary: $summary"
if ($unchecked.Count -gt 0) { exit 2 }
if ($failed.Count -gt 0) { exit 1 }
exit 0
