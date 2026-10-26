# QA replay: clearing the stash desk

The owner's ask of 2026-10-26, after 0.7.0: "We need an option to clear the desk because it quickly gets messy". From the
coordinator's options the owner picked "Button + fresh after raid" (docs/DESIGN.md §4, *The stash desk*, *Clearing the
page*; docs/NEXT.md, "2026-10-26: clearing the desk"): CLEAR, a small amber word at the head line's right, empties the
page in hand for the mode at once, with no question (principle 7): every region back to the dashed frame of a region
never read, the head line as for a page with no part read (KEEP 0 · 0 · 0 alone), the pins and the card pointed at gone.
A one-line notice says "Desk cleared." with UNDO beside it, standing 8 s as notices do; UNDO puts the page back exactly
as it was, its pins with it, while the notice stands and no reading came since. The first desk reading after a raid
starts a new page the same way ("A new desk page after the raid.", with UNDO); this replay can't play a raid's end over
the made-up desk, so that half is held by `DeskClearSessionTests` and the session's own notice.

`desk-clear.devscript` hands the window the made-up desk's reading 1 (`Rules.DeskExample`), pins the stash's LEDX, points
at the Zhuk's cell in the backpack, then plays `desk clear` (CLEAR clicked, through the code the click calls), `desk undo`
(the notice's UNDO clicked), `desk clear` again with the notice waited out, and `desk 1` again. The made-up desk is
emptied in the window (`Rules.DeskClearing.Emptied`); a real page is emptied by the session (`GameSession.ClearDeskAsync`),
which the tests hold. The rules are held by `DeskClearingTests`, `DeskLedgerTests` and `DeskClearSessionTests`; the checks
below are for the stills.

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\desk-clear'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\desk-clear.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
Get-ChildItem "$qa\data\logs" | Select-String -Pattern 'INFO Developer view: desk'
```

Then `--culture de-DE` and `--window 900x560` and `--window 1600x900` added to the arguments for the other runs, each
copied away before the next (docs/QA.md, "Replay scripts").

## What the stills and lines must show

- `before`: the made-up desk as `desk.md`'s `first` has it, and at the head line's right, at the desk's right edge 18 DIP
  in, CLEAR (German LEEREN) in amber capitals at 11 px, on the head line's row, its top level with the head line's, never
  over it and never wrapping; the head line wraps before it where the desk is narrow (900×560), never runs under it. The
  rail: the Zhuk's card, the LEDX's pinned line at its foot.
- `cleared`: the page empty and at its top (the script's pointing had scrolled it at 900×560): LOADOUT's boxes dashed with "NOT IN A SCREENSHOT YET" under its head, every other region its
  head over a dashed frame (TACTICAL RIG, BACKPACK and POUCH an empty slot box beside it), no window, the page's shape as
  `before` had it at that size, nothing dimmed or dated; the head line "KEEP 0 · 0 · 0" alone (German „BEHALTEN 0 · 0 ·
  0“); no CLEAR. The rail: the hint ("Point at an item for its card. …"), no card, no pinned line. At the top, the notice
  in amber's frame: "Desk cleared." (German „Desk geleert.“), then UNDO (RÜCKGÄNGIG) in amber capitals, then the ×. The
  log: "desk: cleared the made-up desk (CLEAR now gone)".
- `undone`: the page as `before`, standing at once (no cell faded, the totals at their full figures), CLEAR back at the
  head line's right; the LEDX's pinned card back in the rail (whole under the hint, the pointed card having gone with the
  clear); no notice. The log: "desk: page put back".
- `expired`: the cleared page again, and no notice (it went after its 8 s, its UNDO with it). The log's next line: "desk:
  no notice offers UNDO".
- `read-again`: reading 1 whole again, as `before`, CLEAR at the head line's right, no pin.
- Nowhere: CLEAR in a raid (the desk is a menu view), a question before the clear, a notice that stays.
