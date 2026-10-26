# QA replay: the grid under the pointer

The owner, 2026-10-26, after trying 0.7.0's squad grid: "When you are with the mouse over the map highlight the current
square and sub-square and show the currently mouse-overed position somewhere where it makes sense" (docs/DESIGN.md §4,
"Map drawing", *The grid*, *Under the pointer*). While the pointer is over the map inside the grid's strips and the grid
is on, the 100 m square under it is lit faintly and its ninth a little more (the grid's own lightening step, no hue, under
every line, symbol, name and route), the strips' letter and number of that square are bold in full ink with the strips a
step lighter along it (`Raised`), and the readout at the map's top left says its reference: "POINTER D4-2" ("MAUS D4-2"),
the only line out of a raid and on one plate under "YOU D5-8" in one. Nothing moves; all of it goes as the pointer leaves
the map. The rules are held by `GridTests` (the reference and ninth under a point, the map's edges, a point over a strip
or off the lettered grid, the fill, the strips, the readout's lines, a plate over it, the pointer on it, the grid off, a
move that lays nothing out) and `DevToolsTests` (the step); the checks below are for the stills.

`grid-pointer.devscript` ends the tour, shows Streets of Tarkov in Plan and moves the mouse over it with `pointer <fx>
<fy>` (fractions of the map's size, through the code the pointer's own moves call) and off it with `pointer leave`, then
starts a PMC raid there with a position in D5 (`pos 40 2.5 120 90`) and does the same.

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
$qa = Join-Path $env:TEMP 'shturmap-qa\grid-pointer'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\grid-pointer.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --window 900x560 --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

- The stills go to `%TEMP%\shturmap-qa\grid-pointer\plan-notice`, `plan`, `plan-moved`, `plan-off-grid`, `plan-left`,
  `raid-you`, `raid` and `raid-left`; then `--window 1600x900` and `--culture de-DE` for the other runs (docs/QA.md,
  "Replay scripts"). `--window` takes pixels: the DIP size times the monitor's scale.
- `--verbose`: the app log (`$qa\data\logs`) has a line per `pointer` step, "pointer: at 225, 216 DIP on the map; square
  D4-2" (at 900×560), with ", the readout's line not drawn (the pointer on it, or a plate over it)" under the notice, "no
  square under it" off the lettered grid, and "pointer: off the map".
- Each folder holds `map.png` (the map alone, as drawn) and `window.png` (the window, its map's panel empty): the map's
  drawing is judged on `map.png`, the notice and the controls on `window.png`.

## What each still must show

- **plan-notice**: "The tour stays in help: F1, then TAKE THE TOUR." at the map's top (`window.png`). On `map.png` the
  square under the pointer lit and its ninth more (at 900×560 D4 and its bottom middle ninth, D4-2), the top and bottom
  strips a step lighter along column D with its letter bold and brighter, the left and right strips along row 4 with its
  number; at 900×560 no readout line at the top left: the notice lies over its place (`PlateOver`; the app log's line
  says "the readout's line not drawn"). At 1600×900 the notice stands clear of it, and the line shows as in **plan**.
- **plan** (the notice gone): the same, and at the map's top left, 8 DIP inside the strips, a plate with a `LineStrong`
  hairline: "POINTER" small in muted ink, then "D4-2" (D4-1 at 1600×900) in ink a step smaller than YOU's figure (17 to
  22), no sand. The
  square references under the plate covered while it shows; nothing else moved from `plan-left` (labels, names,
  symbols, the scale bar where they were).
- **plan-moved**: another square and ninth lit (E7-9 at 900×560, F7-7 at 1600×900), the strips' D and 4 back as the
  others, the new letter and number lit; the readout says the new reference, its plate as wide as before.
- **plan-off-grid**: the pointer at the readout's corner, left of column A on Streets: nothing lit, no readout line, the
  strips as at rest.
- **plan-left**: the map as before the pointer came: no fill, no strip lit, no readout (out of a raid there is no YOU).
- **raid-you**: the raid's map with the position in D5 and its tag "D5-8"; "YOU D5-8" at the top left, YOU muted, the
  reference large in sand; no pointer line, no square lit.
- **raid**: "YOU D5-8" where it stood, and under it on one plate, its hairline running round both lines, "POINTER D4-2"
  (D4-1 at 1600×900) in the smaller ink figure; D4 and its ninth lit, D and 4 lit on the strips; the marker's tag still
  "D5-8".
- **raid-left**: as raid-you, pixel for pixel (and plan-off-grid as plan-left).
- **1600×900**: the same moments, squares larger (the fill reads more plainly; the ninth's lines shown or ticks, by
  zoom), the readout the same size in DIP.
- **German**: "MAUS" for POINTER, "DU" for YOU; the references as they are.
- **Not here**: a lit square under the in-raid glance's plate (`tools\qa\glance.devscript` with a `pointer` step
  added: the readout's lines not drawn under the plate, the fill and strips still), the sheet (The Lab's: `shturmap-cli
  render the-lab <out> --sheet --pointer 0.45 0.45`), and the fill's strength, which `render --pointer` measures.
