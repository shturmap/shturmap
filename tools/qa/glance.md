# QA replay: the in-raid glance's plate

The owner's three choices of 2026-10-19 (when, what and what is kept), and from the glance's panel Variant 1, "with the
looted container's or body's item layout mirrored" (docs/DESIGN.md §4, "Screen anatomy", *The in-raid glance*): a
screenshot taken in a raid that shows an inventory screen is read as it arrives, after its extract list, and a plate
at the top of the map's column says what the loot is worth: FROM YOUR SCREENSHOT, "LOOT 734k+ ₽" large with the total
in amber, "ON YOU 4.63 M+ ₽ · 1 unsure" under it, then the LOOT pane's regions in the stash desk's cells (*The cell*),
each under its own head, as the game shows them: a container's grid; a body's slots, its rig, pockets and backpack, a
part not searched a closed region with "UNSEARCHED +". The player's own regions are never drawn. It stands 6 s, then
fades out over 0.4 s; a newer reading replaces it at once; a reading that comes while a cue is up waits for the cue to
go; the raid's end takes it. Nothing on it takes a click, a pointer or a tip (no click in a raid, principle 1).

`glance.devscript` replays it without a screenshot: its `glance <n>` hands the window the made-up reading n
(`Rules.GlanceExample`, built through the model as a raid screenshot's reading is, from the made-up desk's items, facts
and needs): 1, a drawer with three items (a graphics card, a wallet whose contents are unseen, a keycard that is one of
three); 2, a body (its slots: a helmet and an armband that are "?", body armour at 98/147, a dog tag, an AK and a Glock
with their mods unseen; its rig UNSEARCHED; its pockets SEARCHING with an Analgin found; its backpack searched: a
Grizzly at 1469/1800, a graphics card, a LEDX, a moonshine, a bitcoin, roubles). The rules are held by
`GlancePlateRulesTests` (what is drawn, the head's words, the regions' heads and states, the sizes and the cut) and
`DevToolsTests`; the checks below are for the stills.

## Run it

On a developer build (Debug, or the dev build) of the commit under test, from the repository's root, in PowerShell:

```powershell
.\eng\dotnet.ps1 build src\Shturmap.App\Shturmap.App.csproj
$qa = Join-Path $env:TEMP 'shturmap-qa\glance'
if (Test-Path $qa) { Remove-Item -Recurse -Force $qa }
$exe = (Get-ChildItem src\Shturmap.App\bin\Debug -Recurse -Filter Shturmap.exe | Select-Object -First 1).FullName
$script = (Resolve-Path tools\qa\glance.devscript).Path
$app = Start-Process -FilePath $exe -PassThru -ArgumentList "--dev-view --verbose --data `"$qa\data`" --dev-script `"$script`""
$app.WaitForExit()
```

- The stills go to `%TEMP%\shturmap-qa\glance\glance-drawer`, `glance-body`, `glance-gone`, `glance-waits`,
  `glance-after-cue` and `glance-raid-over`; then `--culture de-DE`, `--window 900x560` and `--window 1600x900` for the
  other runs (docs/QA.md, "Replay scripts").
- `--verbose`: the app log (`$qa\data\logs`) has the lines `Example glance: reading n of 2` and `In-raid glance:
  reading n, a LOOT pane, 4 regions at 80 DIP, 0 cut` (how many regions stand, at which cell size, how many gave way to
  the line under them).
- `tools\fake-raid.ps1 -Glance 1` (or `2`) puts the same plate up in a fake raid's snapshot, which keeps it up.
- Windows' animation effects on: with them off the plate goes at once at 6 s instead of fading.

## What each still must show

- **glance-drawer**: at the top of the map's column, centred, 16 DIP under its top (8 under a notice where one
  stands), the plate: the ground at 90 % between two amber hairlines, no frame at its sides; FROM YOUR SCREENSHOT in
  muted capitals; "LOOT" in ink and "734k+ ₽" in amber at the title's 48 px, one line; "ON YOU 4.63 M+ ₽" in ink and
  "· 1 unsure" muted under it; then DRAWER in the desk's head style over a grid of four by two: the graphics card (2×1)
  with the price tag and "630k", an open amber notch (next) and "↑ +14%" (from 56 DIP); the wallet "46k+"; the
  keycard's "?" plate, its art at half strength, "160–459k". The cells at 80 DIP at the large window (as many
  pixels as the desk's at 80), the grid's empty cells the panel's ground with hairlines. The rail untouched, the
  floor picker gone while the plate stands (it is back in glance-gone). No red or green on the plate.
- **glance-body**: the drawer gone, the body in its place at once: "LOOT 3.06 M+ ₽", "· 2 unsure"; BODY over the
  slot cells, two to a row (80×40 a slot at 40 DIP, larger with the cells), the helmet and armband with the "?" plate
  and a range, the armour "~74k" over "98/147", the dog tag after the person glyph, the AK and the Glock "~…k+"; TACTICAL
  RIG over the rig's slot cell and a closed region, the cells' ground in a strong frame, "UNSEARCHED +" in its middle;
  "POCKETS · SEARCHING +" over four pockets, the Analgin in the second; BACKPACK over its slot cell (the trader's
  portrait before "63k") and its grid of five by four (the Grizzly "~33k" over "1469/1800", the graphics card, the LEDX
  "1.00 M" with a filled amber notch, the moonshine with a bracket, the bitcoin with a filled notch in ink, the roubles'
  "50000"). In reading order, flowed into columns where one would pass the plate's height; the plate no taller than
  about half the map's column; no region cut at the large window and at 1600×900 (48 DIP cells there). None of the
  player's own gear (LOADOUT, the rig with the Black Rock, the stash's items) on the plate.
- **glance-gone** (6 s after glance-body): no plate; the floor picker back.
- **glance-waits** (1 s after the extract list was read and a reading came): YOUR EXTRACTS THIS RAID's roll call in the
  map's middle, as ever, and no plate: the reading waits for the cue to go. The app log says "glance: reading 1 (waiting
  for the cue to go)".
- **glance-after-cue** (8 s later): the roll call gone, the drawer's plate standing as in glance-drawer.
- **glance-raid-over**: Plan after the raid, no plate.
- **900×560**: the drawer at 40 DIP whole; the body's regions no room even at 40 under the title, so the plate is its head
  and the line "ALSO · BODY 134k+ ₽ · TACTICAL RIG 41k+ ₽ · POCKETS 2.3k+ ₽" with "· BACKPACK 2.89 M ₽" on its second
  line: where the line wraps, the next starts with "·", never a line ending in one. The plate within the map's column,
  above the map's buttons, no taller than about half the map.
- **German**: AUS DEINEM SCREENSHOT, "LOOT 734k+ ₽" (the decimal comma, "3,06 M+ ₽"), "AN DIR 4,63 M+ ₽ · 1 unsicher";
  RIG, TASCHEN · WIRD DURCHSUCHT +, RUCKSACK, NICHT DURCHSUCHT +; BODY and DRAWER as the English game prints them;
  "DAZU · …" at 900×560.
