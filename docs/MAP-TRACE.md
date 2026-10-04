# Floor plans for the maps without SVG artwork

**Status: on hold** (owner, 2026-10-04: "Write this up and put in on hold for now"). Nothing in the app changed:
The Lab, Labyrinth and Icebreaker are drawn from tarkov.dev's renders as before. Don't take this up again, and
don't propose it, until the owner says so.

The question (owner, 2026-10-04): "Is it possible we can convert the maps where we do not have great quality like
labs to convert it to the same style and color coding as the ones where we have proper maps, like customs? If you
say it's possible, put instructions for a less capable model so it still works. We should only do this if we are
sure that we can achieve high quality, crappy maps will defeat the whole purpose of the project."

## Where this stands

**The answer so far: not sure, so nothing was built into the app.** Two things are known, one is not:

- *Known:* a floor plan to scale can be traced out of the renders by machine and measured. On The Lab's main level
  it came out with straight walls and the right rooms at the first try.
- *Known:* that alone is not a map of the quality of the hand-drawn ones. The traced plan has floor and not-floor
  only; obstacles, stairs and landmarks are what a cartographer chooses, and a plan shows less than today's render.
- *Not known:* whether a finished plan would look good enough. No finished plan was made, and none was seen in the
  app with markers and names on it.

**What exists.** `tools\map-trace\stitch.cs` and `trace.cs` (both run; they are in no build and no test), this
file's steps and gates, and the trial's pictures outside the repository (render beside plan, for The Lab's main
level and Labyrinth). No SVG is in the repository, the trace read only tiles already in the cache, and DESIGN.md
says nothing of it.

**What was found on the way.** re3mr has hand-drawn plans of Labyrinth and Icebreaker under CC BY-NC-SA 4.0
("Other maps that exist", below). They are the likelier base for those two maps than the renders. For The Lab
there is no such plan.

**When this is taken up again, in this order:**

1. *The owner looks at the trial's pictures of The Lab:* is a plan like this, cleaned, wanted at all, or does the
   render serve better? A no ends it for The Lab.
2. *For Labyrinth and Icebreaker, the owner decides on re3mr's plans as the base:* it makes the result CC BY-NC-SA
   4.0 and lifts rule 1 for those two maps. A word with re3mr comes first.
3. *Then the measuring, which costs little:* step 5's four clean-up rules on The Lab (written down, never run),
   and whether re3mr's plans are to scale (fit one to the data's extracts and spawns, then step 6's gates against
   the render's floor).
4. *Only then steps 7 to 9.* The map keeps its render unless every gate passes and the owner approves the pictures.

**Not checked:** the terms of the other sites that show maps of their own; whether the label layers come off
re3mr's Photoshop files; whether Labyrinth's plan of March 2025 still matches the level; The Lab's two other
levels; how a plan reads in the app.

The rest of this file is the instructions as they were written for whoever continues, a less capable model
included: every step ends in a number or a picture. `docs/DESIGN.md` stays the binding spec; nothing here changes
it until the owner decides (step 9).

## What was tried, and what it showed (2026-10-04)

The Lab, Labyrinth and Icebreaker are drawn from tarkov.dev's tile renders (DESIGN.md §3, "Maps without SVG
artwork"). In those renders everything that isn't floor is transparent or near black, walls included. So "drawn
and not near black" is the floor, and its outline is a floor plan to scale: one pixel of zoom 4 is 7 cm on The Lab.

`tools\map-trace\stitch.cs` and `trace.cs` do that. Run on the tiles already in the cache:

| | The Lab, main level | Labyrinth |
| --- | --- | --- |
| outline on an axis | 92.7 % | 62.5 % |
| plan differs from the floor mask | 0.84 % of the floor | 2.12 % |
| differs beyond 2 px of an edge | 0.003 % | 0.012 % |
| specks and crumbs cleaned away | 0.46 % | 2.06 % |
| how it looks | walls straight, rooms right; marks left by cars and dark props; the round opening is a polygon | ragged: dark stains and painted lines come through, curved corridors are jagged |

What this proves: the geometry can be had by machine, to scale, and measured. What it does not prove: that the
finished map looks as good as the hand-drawn ones (Factory is the nearest in kind). A hand-drawn map chooses what to
show: obstacles, stairs, landmarks. The traced plan shows floor and not-floor only, and where the render tells rooms
apart by floor colour or glass, the plan has one area.

So, by map:

- **The Lab: worth finishing as a trial.** Three levels, right angles nearly everywhere.
- **Labyrinth, from the render: only after The Lab passed**, and only if step 5's rules clean it without hand work.
  Expect it to fail.
- **Icebreaker, from the render: don't.** Sixteen deck layers on one narrow ship, in a render stretched 1.75 times
  along one axis.
- **Both, from re3mr's plans: open.** See "Other maps that exist".

A side effect worth having: a map with a plan of its own asks tarkov.dev for no tiles, so what PRIVACY.md and
DESIGN.md §2 say about tile requests following the view no longer applies to it.

## Rules that hold in every step

1. **Sources.** Only tarkov.dev's tile renders, read from the app's cache, plus maps.json. Never the game's files,
   memory or process (DESIGN.md §2). Never another artist's file: the svg-maps repository's `Labs.svg` is not a
   base and is not copied from (it isn't to scale, and its licence is not ours to mix in).
2. **Load on tarkov.dev.** Read tiles from `%LOCALAPPDATA%\Shturmap\cache\map-tiles\<map>\<layer>\4\`. Fetch only
   tiles that aren't there, each once, one at a time, 250 ms apart, with the app's User-Agent; stop at the first
   answer that is neither 200 nor 404; never fetch a file that exists. A level at zoom 4 is at most 256 tiles and
   about 8 MB. Say the count before fetching. Users' copies of the app never fetch for this.
3. **What goes into the repository.** The scripts, and after step 9 the SVG. Never tiles, stitched renders, masks
   or plan pictures: they are, or come straight from, Battlestate's art. Work in a folder outside the repository.
4. **Only what the render shows.** No door, stair, room or wall is added by guess (DESIGN.md: Shturmap shows only
   what it read). If the render doesn't show it, the plan doesn't have it.
5. **No drawing by hand.** Every change to the plan is a rule in `trace.cs` that applies to the whole map, with a
   comment that says why. No editing of the SVG's numbers, no rule that names a place ("at x = 1200…").
6. **Stop means stop.** Where a step says stop: write what was measured, save the pictures for the owner, change
   nothing in the app. Don't loosen a gate, and don't try a way around it that isn't in this file.

## Steps

**1. Read first.** DESIGN.md §3 ("Maps without SVG artwork", "The sheet"), §2 ("Never share a position"), and
`CLAUDE.md`. Print the map's entry in maps.json: `tilePath`, each layer's `tilePath`, `tileSize` (256 when it gives
none), `transform`, `bounds`.

**2. Tiles.** For the base layer and each floor layer, zoom 4. The tile `(x, y)` covers map units
`[x·T/16, (x+1)·T/16]` each way, T being `tileSize`; the tiles needed are those that meet the map's world rectangle
(`MapProjection.WorldRect`). Fetch what is missing under rule 2.

**3. Stitch.** `.\eng\dotnet.ps1 run tools\map-trace\stitch.cs '--' <cache folder of the layer>\4 <work>\<layer>.png`
It prints the tile range. More than 5 % of the range "not there" inside the map's rectangle: stop.

**4. Trace.** `.\eng\dotnet.ps1 run tools\map-trace\trace.cs '--' <layer>.png <layer>.svg <layer>-plan.png <tileSize> 4
<smallest x> <smallest y> <group id>`. The group id is the floor's name with underscores (`First_Level`). It prints
three lines that start with `GATE`.

**5. Finish, by rules only.** Look at `<layer>-plan.png` beside `<layer>.png` at full size, in at least six places.
For each kind of fault below, add its rule to `trace.cs` (none of these was tried yet; each must leave the three
gate numbers passing):
   - *Marks of cars, crates and stains:* a hole that is smaller than 6 m² and has less than 80 % of its outline on
     an axis is not a wall: it becomes floor.
   - *Ticks at door frames:* two points less than 0.4 m apart that leave a line and return to it go.
   - *Round things:* a ring whose points all lie within 0.15 m of one circle is written as a circle.
   - *Slanted walls:* an edge within 8° of 45° lies on 45°.
   A fault that none of these fixes, and that shows at the zoom where a room fills a quarter of the window: stop.

**6. Gates, per level.** All must hold:
   - differs beyond 2 px of an edge: at most 0.02 % of the floor;
   - differs from the mask: at most 1.5 % of the floor;
   - on an axis: at least 90 % (The Lab). For a map with curves there is no number: its curves must be smooth
     at the zoom above, or stop;
   - from the data (write this as a test that reads the local cache, as the synopsis tests do): of the level's
     loot containers, extracts and player spawns, at least 97 % lie on floor in the plan. Under that: stop, the
     plan or its placement is wrong.

**7. One SVG per map.** One top-level `<g id="…">` per level, the base level first; inside it `<g id="Floor"
class="floor">`. The `<style id="style_common">` block carries the svg-maps repository's class names and colours
(`.floor { fill:#70777f }` and, only if the owner asks for more than floor, `.building`, `.stairs`, `.locked`), so the
app's colour treatment (`ArtworkColors`) meets what it meets on Factory. No text, no raster. The path numbers are
map units already. Set the `viewBox` to the map's artwork rectangle in map units (`MapProjection.ArtworkRect`:
left, top, width, height); that changes no path.

**8. Pictures for the owner, before any app code.** Render and plan side by side: each level whole, and four
details at full size. Save them where the owner's pictures go, outside the repository. Then stop and ask. The
question to answer is the owner's: is this as good as the proper maps? If the answer isn't a clear yes, the map
keeps its render and the work ends here.

**9. Only after a yes: the app.** The owner decides first (see below). Then, with DESIGN.md §3 changed in the same
commit:
   - the SVG ships with the app, and the map's definition is given the SVG and each level's group id where the
     definitions are read, so every later step treats it as an SVG map (`MapDefinition.SvgPath`, `MapLayer.SvgLayer`,
     `FloorResolver`);
   - `ArtworkProvider` loads the shipped file instead of downloading, and asks for no tiles for that map;
   - a test: `MapProjection.PlaceSvg` for the shipped viewBox gives scale 1 and offset 0, so plan and data share
     one coordinate system;
   - the credit line on the map (`MainWindow.xaml.cs`, where "Map ©" is written) says the truth for this map, in the
     words the owner chose: not Shebuka's licence line, not "Map: Tarkov.dev";
   - snapshots (`tools\fake-raid.ps1 -Map lab`) of each level: extracts in their rooms, room names on their rooms,
     the player on the floor shown. Compare with a snapshot of the render from before;
   - README credits, PRIVACY.md and DESIGN.md §2 (the tile sentence no longer names this map), help's legend if a
     symbol changed, `docs/NEXT.md`.

## Other maps that exist (looked up 2026-10-04)

Owner: "Are there other maps for labyrinth and icebreaker out there that might help?"

- **re3mr (reemr.se) has hand-drawn plans of both.** Labyrinth (version 0.6D, March 2025, one level, about
  118 × 109 m) and Icebreaker (version 1.7, 2026, fourteen decks drawn side by side, built from a model of 3,314
  objects in Blender, about 34 × 168 m). Flat colours and clean walls; labels, icons and legends are drawn on top,
  and each map comes with its Photoshop file. Licence: CC BY-NC-SA 4.0, the licence of the SVG maps the app shows
  already. maps.json names them as these maps' 2D versions (`labyrinth-2d`, `icebreaker-2d`). re3mr has no map of
  The Lab.
- **No SVG in the svg-maps repository's style exists for either**, and that repository has no branch, pull request
  or issue for them (its last addition is Terminal). Other sites (Tarkov Market, gamemaps.net, tarkovwiki.com) show
  interactive maps of their own or re3mr's; their terms weren't checked.
- **What they could be.** The plan to trace in place of the render: flat colours trace cleanly, which is what failed
  on Labyrinth, and Icebreaker's decks are already told apart. And the guide to what a thing is: stairs, locked
  doors, chambers.
- **What isn't known.** Whether they are to scale: only the published pictures were looked at. To measure it, fit a
  plan to the data's extracts and spawns, then run step 6's gates against the render's floor. Whether Labyrinth's
  plan from March 2025 still matches the level. Whether the label layers come off cleanly in the Photoshop files
  (another project's README says it removed layers from them).
- **What it would mean.** A map made from them is an adaptation: CC BY-NC-SA 4.0, credited to re3mr, not for
  commercial use, and not under Shturmap's MIT. Rule 1 holds until the owner decides. Asking re3mr first is the
  decent way, and they may have each deck as an export to scale.
## For the owner to decide before step 9

- **Is the plan good enough?** From step 8's pictures. A plan shows less than the render (no cars, racks or floor
  colours to find one's way by); room names and markers read better on it.
- **Whose drawing is it, and under which licence?** It is traced from tarkov.dev's render of Battlestate's level,
  as every community map is from the game. In the repository it would be Shturmap's own file. Offering it to the
  svg-maps repository instead is possible too; its guidelines ask for a discussion first.
- **Does the render stay as a choice** for that map, or does the plan replace it?
- **re3mr's maps as the base for Labyrinth and Icebreaker?** See above: it changes the licence of the result and
  rule 1.
- **More than floor?** Obstacles, stairs and locked rooms are what make Factory's map read well. They can't be
  traced by the rule above; drawing them is hand work, or a next trial.
