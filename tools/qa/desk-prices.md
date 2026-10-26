# QA replay: the desk's pictures across a price update

The owner's decision of 2026-10-26 ("When finding prices in the stash it's important that they are recent"): while the
desk is on screen its prices are asked for again every five minutes, and what comes in prices the page again where it
stands, quietly (docs/DESIGN.md §3 and §4, *The stash desk*, *Fun*). The page is built again for it, and every cell's
picture must stand in the first frame after, never blank while its file is decoded again: a page built again draws
each picture from the bitmap the page already has (`DeskBoard.ArtOf`). A reading that rebuilds the page does the same.

`desk-prices.devscript` hands the window the made-up desk (`Rules.DeskExample`), then `desk probe 3` (a reading that
changed nothing), `desk prices` three times (the flea's prices a tenth up each time, the same reading) and `desk probe 2`
(a reading that changed the backpack alone). Each of those steps renders the desk in the frame before it and asks for
the first frame after it at once (`DeskBoard.PicturesDrawnAsync`, a RenderTargetBitmap of the desk, never the screen),
and judges each cell with a picture in the page's view by the middle of its art: drawn where its pixels vary, blank
where they are the cell's flat ground. A cell coming in (a reading's, its opacity from 0) is counted apart.

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\desk-prices'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\desk-prices.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
Get-ChildItem "$qa\data\logs" | Select-String -Pattern 'INFO Developer view: desk'
```

## What the lines must say

- `desk probe 3` and each `desk prices`: "in the first frame after: N of N", every picture judged drawn, as many as
  in the frame before (52 at the work area's size on 2026-10-26; one more after reading 3 than reading 1, its LEDX).
  Before the bitmaps were kept (2026-10-26) the first frame after new prices drew 3 of 52.
- `desk prices`: the totals rise ("totals a/b -> c/d", the flea's prices up a tenth), the reading the same: nothing on
  the page comes in, its head line's totals don't count up.
- `desk probe 2`: the backpack's cells "coming in" (their entry, as before), every other picture drawn.
- The stills `before` and `priced` in `%TEMP%\shturmap-qa\desk-prices`: the same page, its figures and totals a tenth up
  where the flea is the better sale.
