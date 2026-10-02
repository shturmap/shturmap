# Shturmap design

This document is binding for anyone changing Shturmap, people and agents alike. Read it before you change
behaviour or UI, follow it, and update it in the same change when a decision changes.

## 1. What Shturmap is

Shturmap answers two questions for an Escape from Tarkov player, on a second monitor:

1. **Before a raid: where should I go, and what do I need to bring?**
2. **During a raid: where am I, and where is my next objective?**

That is the whole product. A feature belongs in Shturmap only if it helps one of these two questions and works
without the player having to click during a raid. When in doubt, leave it out.

**Not goals** (declined on purpose; don't add them): item prices or a flea-market view (a price appears only as
part of "where to get" an item a quest needs), hideout tracking, loot or container maps beyond what quests need,
item scanning by hotkey, ammo charts, squad sharing, achievements, overlays on the game window, statistics
dashboards, a settings maze.

## 2. Ground rules (game terms of service)

Shturmap reads only files the game writes for the player, plus public community data:

- screenshot **file names** in `Documents\Escape from Tarkov\Screenshots` (positions only; Shturmap never opens the
  images);
- `application_*.log` and `push-notifications_*.log` in the game's `Logs` folder, with shared read access;
- `Control.ini` and `Game.ini`, read-only.

It never opens a handle to `EscapeFromTarkov.exe`, reads or writes its memory, injects, hooks, sends input,
registers global hotkeys, captures the screen, draws over the game, edits game files (including
`Logging.config`), or reads `backend_000.log` or launcher credentials (only `gamesRootDir` is read from the
launcher settings). `SafetyTests` fails the test run if forbidden APIs appear in `src/`. Keyboard shortcuts work
only while Shturmap's own window has focus.

BSG's licence agreement (4.3.4, 2018 text), read literally, covers all companion tools; the owner accepted that
residual risk. Do not widen the boundary.

## 3. Copyright and data use

- **Shturmap's own code** is MIT-licensed (`LICENSE`; owner, 2026-10-02, to publish it on GitHub). Everything a
  build redistributes is listed with its licence in `THIRD-PARTY-NOTICES.md`; `eng\notices.ps1`, run by the app
  project before every publish, gathers it, `LICENSE` and every package's own licence files into the build's
  `licenses` folder, which the single exe carries inside and unpacks with the app (§8, "Distribution"). The help
  panel's LICENCES link opens it. Update it in the same change as any package. Everything Shturmap uses but doesn't ship (data, artwork, ideas) is credited in the README.
- **Game data** (quests, maps, items, extracts, bosses) comes from tarkov.dev's public JSON service
  (`json.tarkov.dev`), run by The Hideout. Its API page says it is free with no rate limit; there are no written
  terms. Requests say who they are (User-Agent `Shturmap/…`) and are cached with ETags (1 h, 24 h, 7 days by kind).
  Data is downloaded at runtime into the user's cache for personal use and shown with "data tarkov.dev". It is
  never bundled in the repository or a build.
- **Map geometry** comes from tarkov.dev's `maps.json` (MIT, Copyright (c) 2019 Oskar Risberg; the test fixture
  snapshot carries that notice).
- **Map artwork** is the SVG maps by Shebuka and contributors (CC BY-NC-SA 4.0). Their README revokes the licence
  for software "designed to facilitate cheating or gaining an unfair advantage" (radars and ESP overlays, maps
  modified for cheat clients, automation scripts, pixel-bots). Shturmap is none of these: positions come only from
  screenshots the player takes, the game is never touched and nothing is automated (§2). The artwork is downloaded
  at runtime, displayed unmodified except for showing one floor at a time (their README invites that), credited on
  the map with its licence, and never redistributed. Non-commercial only. A published screenshot that shows a map
  shares the artwork: credit it the same way and keep it non-commercial.
- **Maps without artwork** (The Lab, Labyrinth, Icebreaker; checked 2026-10-02). tarkov.dev shows them as image
  tiles (assets.tarkov.dev, credited to "Tarkov.dev" and to TarkovBOT.eu for Icebreaker) that are top-down renders
  of the game's own level geometry and textures, i.e. Battlestate's art, which Shturmap doesn't use (below). The
  svg-maps repository's `Labs.svg` (Shebuka, CC BY-NC-SA 4.0, last changed 2023) is a schematic that isn't to scale:
  fitted to the georeferenced level, its rooms are off by 13–22 m, so positions would land in the wrong rooms, and
  tarkov.dev has never placed it either. These maps are drawn as a **sheet** instead, from data only: maps.json's
  bounds (the extent tarkov.dev gives the map, not a traced outline) as a panel with a metric grid on whole metres
  (10 m, every fifth line stronger), tarkov.dev's labels for the shown floor, and everything else as on any map
  (player, trail, objectives, extracts, transits, spawns, floors). No walls or layouts are drawn: none exist in the
  data (owner, 2026-10-02: Shturmap shows only what it has a precise readout for). The sheet says
  "NO ARTWORK FOR THIS MAP · GRID 10 M" in its corner (plus "NO FLOOR DATA" where maps.json has no floors, as for
  Labyrinth: its markers are then all on one plane), the credit line says so, and a notice says it once per map.
  A sheet uses one scale for both axes (`MapProjection.For`), so its squares stay square; tarkov.dev's Icebreaker
  transform stretches Y 1.75× to fit its tiles. The credit line names an artist only where their SVG is drawn.
- **Trader portraits and item icons** are Battlestate's art. They are never in the repository, a build or the
  test fixtures. Shturmap fetches each one from tarkov.dev's image service (`assets.tarkov.dev`) the first time it
  is shown, keeps it in the user's cache (`cache\game-art`), and shows a glyph when it can't be had. This is
  display for personal use, the way every Tarkov tool and tarkov.dev itself show them. Screenshots of Shturmap
  on the website and in the README may show them, as the wiki and other community sites do (owner, 2026-10-02,
  accepting that BSG's licence 4.2.2, read literally, doesn't allow it); they are never published on their own.
- **Not used**: other Battlestate artwork (quest images, game UI art, map art), text from the EFT wiki or guide
  sites (the quest card links to the wiki page instead), and code from other community tools: TarkovMonitor,
  TarkovTracker and MAYAK (GPL-3.0), Tarkov Pilot (no licence), RatScanner (source-available, based on the Elastic
  License). Facts learned from them (log formats, file paths) are reimplemented. The idea comes from TarkovEyes
  (MelGP, MIT); Shturmap is a from-scratch rebuild and takes no code from it.
- **Name and branding**: no "Tarkov" in the app's or the repository's name, no Battlestate logos, typefaces or UI
  art. Logo lettering is drawn from scratch (Windows fonts may be used on screen, not converted to outlines). The
  README says Shturmap is unofficial and that game content belongs to Battlestate Games.
- **Icons** are the Segoe Fluent Icons font that ships with Windows, used in place, never copied into the repo;
  the only custom glyph is the crosshair (drawn from a path in `Shturmap.Map.Glyphs`).
- **Test fixtures** contain only scrubbed logs (`tools/make-log-fixtures.ps1`) and the maps.json snapshot. Quest
  texts are not committed: the synopsis tests quote a few short objective lines, and the check over all quests
  reads the user's local tarkov.dev cache.
  `tests/fixtures/ocr` is ignored by git: it holds the owner's own Tasks screenshots from the removed OCR feature.
  Never commit or publish it; it was removed from the whole history on 2026-10-02, before going public.

## 4. UX principles

1. **No clicks in a raid.** Shturmap follows the game: the logs decide the view, the map, the mode and the side;
   screenshots move the player. Nothing in a raid requires input. Defaults must be right without configuration.
2. **Glanceable from a second monitor.** Primary text ≥ 14 px, numbers in a monospaced face, high contrast on a
   dark ground, the important line first. No animation beyond what helps the eye follow a change.
3. **One window, two states.** *Plan* while in the menus, *Raid* while loading or in a raid. The switch is
   automatic. No tabs, no modes to pick, no modal dialogs. The only other windows are quest cards the player
   pinned; they are owned by the main window and never topmost, so they can't cover the game.
4. **Minimal surface.** Every control earns its place. Prefer an automatic behaviour over a button, a sensible
   default over a setting. Messages are one line and dismiss themselves.
5. **Clear requirements.** What a raid needs (keys, items to bring) is shown before the raid, unprompted, and next
   to the objective that needs it during the raid.
6. **Says why.** Every quest state can say where it came from (the game log and when, or implied by a later
   quest).
7. **Never ask.** No confirmations, no prompts. Quest states come from the game's logs alone (owner, 2026-10-01:
   players don't screenshot their Tasks screen, so Tasks-screen reading and the TarkovEyes import were removed).
8. **Position is occasional.** Players press the screenshot key now and then, not continuously. Everything except
   the "you are here" parts works without a fix. A fix shows its age; the marker stays at full strength and says
   it (see "Map drawing"). No "you may be
   anywhere in here" ring: it was tried and is visual noise. Directions relative to your facing
   ("ahead-left") are shown only for 45 s after a fix; after that they become map directions ("NE", map-up is
   north), which stay true while you move. Distances say how old they are. **A new fix never moves the view**
   (owner, 2026-10-01; the study log showed the player zooming back out within seconds of every automatic
   framing): the map stays where the player put it, and the new position pings instead, three bold sand rings
   leaving the marker over 2.6 s (two still rings with animation effects off). Out of view, a badge at the edge
   of the map points to it with an arrow and pings, says "YOUR NEW POSITION · PRESS F", and a one-line notice says
   so; the badge stays (quietly) while the position is out of view, and clicking it, F, or the map button shows
   the position at the current zoom. An old screenshot found at start doesn't ping. When the raid ends, the
   player marker and trail go: out of a raid there is no "you". In a raid, a position older than 2 minutes (or
   none a minute in) is said at the top of the map in big gold type, "POSITION 7 MIN OLD · PRESS PRTSC OR HOME
   FOR A NEW ONE", and the banner pops once when the window gets focus (the study log: about one position per 8
   raid minutes, often several minutes old when the app was looked at).
11. **Show when the view changes by itself.** When Shturmap changes its view without being asked (a raid
    loading, a transit, a Scav raid starting, the raid over, loading cancelled, the group picking a raid), a cue
    holds the middle of the map for 5 s (owner, 2026-10-01; first 2.8 s, then longer, sharper and with more pop). Its
    entrance plays at 1.8 times its first pace, about 1.6 s (owner, 2026-10-02: the elements should appear more
    slowly; a briefly tried 8 s RAID LOADING cue was too long): a dark band springs open
    behind the text with a short gold flash, gold rules shoot out from the centre with a slight overshoot, and the
    map's name (or RAID OVER) slides up while it decodes letter by letter like a terminal, undecoded letters
    flickering in gold and settling in ink, with one line under it (what the raid can do, or the next raid
    suggested); then it fades. Nothing with text in it is ever scaled, so the text stays sharp. It takes no clicks. Picks the player makes (a Plan card, the map
    list) show no cue. With animation effects off it shows and goes without motion. Never during the log replay
    at start.
9. **Say it before it matters.** When a raid starts loading, a one-line notice repeats what to bring for that
   map, while there is still time to back out of matching. In a group it comes earlier: when the leader picks a
   raid (the notification log says so 20–70 s before loading starts), Shturmap shows that map with the GROUP PICKED
   cue and says "Your group picked Streets of Tarkov · bring: …" (owner, 2026-10-02). The pick's time of day
   ("CURR" or "PAST", the two raid times 12 h apart) isn't shown: the clock formula checks out against the
   screenshots' raid clocks, but which of the two times each name means couldn't be checked without a group raid
   with a screenshot; the study log now records both to settle it. What a quest needs is also beside its name wherever
   it is listed for a map (Plan cards, the raid card): up to three tiny inventory cells with the items' icons,
   "+N" for more; a quest that needs nothing shows one empty, dashed cell, the inventory's way of saying "nothing
   here" (owner, 2026-10-01: BRING alone didn't show which item is for which quest). ANY MAP rows show no cells:
   bringing doesn't apply there.
10. **Two clicks, two meanings.** A click on a quest keeps its card open. Its **highlighter** (a pen, on the row
    while the quest is pointed at or kept, and always on its card beside the pin) keeps it lit on the map (owner,
    2026-10-01: one click doing both was misleading; the study log had the player toggling quests on and off and
    losing held cards on the way to the map).
12. **Point, don't navigate.** Pointing at a quest, an item or an extract anywhere lights up every other place it
    appears (rail rows, cards, map markers) and, for a quest, shows its card. Details come to the pointer; there
    are no detail pages to open. Finished quests appear nowhere.

### Visual language

The game's own UI, pared down: what a Tarkov player already reads at a glance, with nothing added for show.
No stock Windows look (no Mica, no rounded Fluent controls, no pills).

- **Shape**: square corners everywhere (`ControlCornerRadius` 0), flat near-black panels, 1 px hairlines; the
  selected/expanded thing is one step lighter with a stronger hairline, like a selected slot in the game.
- **Type**: Bahnschrift (ships with Windows, DIN-like, close to the game's lettering). Labels and status words are
  semi-condensed, semibold, uppercase and spaced (`EyebrowText`, `StatusText`); titles semi-condensed semibold
  (`TitleText`); body text regular; numbers semibold gold (`FigureText`). Styles live in `App.xaml`; cards and
  flyouts set the font themselves (popups don't get the app's implicit text style).
- **Pictures**: trader portraits square in a thin frame; item icons in a dark inventory cell.
- **Map artwork recedes**: it is drawn at 38 % of its colour and 85 % of its brightness, so its own yellows and
  browns don't compete with the quest amber and Shturmap's markers are found at a glance, while streets and
  buildings still read by brightness. Colours near the amber then lose the rest of their chroma
  (`ArtworkColors`, a small SkSL colour filter: in CIELAB, within 28° of the amber's hue and 14 of its lightness
  the chroma drops to 6, easing out by 55° and 26; cartography review, 2026-10-02): after the plain recede Customs'
  dashed lines were ΔE 3.9 from the amber and 4.1 % of Factory's artwork within ΔE 10; now no pixel of any SVG
  map, floors included, is within ΔE 17 (measured at 2400 px across the sheet). The yellow shapes keep their
  outline and brightness and lose their hue (what they mark in the artwork is unverified). Every map symbol
  carries a dark collar (3 px, ground at 67 %, round at the corners) that gives it an edge on light streets, where
  amber, green and road are nearly as bright (owner, 2026-10-02: unhighlighted quest markers were hard to find
  pre-attentively; the cartography review the same day gave extracts, transits, bosses, snipers and Scav zones the
  same collar, since they measured below 3:1 against mid and light streets). Hollow symbols get a collar band
  under the ring, their middle left open.

| meaning | colour |
| --- | --- |
| quests, objectives, distances, "on" (one accent) | muted gold `#C9AD62` |
| extracts (PMC), success, healthy inputs | green `#8DA65E` |
| Scav extracts | teal `#6F9A94` |
| shared extracts | khaki `#B7B77A`, the triangle split down the middle |
| the player and their trail | sand `#E9E2C8` |
| transits | violet `#9C8CC4` |
| bosses, danger | red `#B8604A` |
| text | beige-white `#D9D5C4`, secondary `#8A8778` (also done objectives on the map, with a check mark) |
| ground, rail, panel, raised | `#0B0C0B`, `#101110`, `#151614`, `#1E1F1B`; hairlines `#2A2B27` / `#45463F` |

Quest **types are shown by glyph, never by colour**; colour stays free for state (open, done, selected).
The type glyph is always the first thing on a quest's row and the only thing inside its map marker. **Trader
portraits** are secondary: small, at the right end of Plan rows, before the quest line of Raid rows and in the card
header; never in place of the glyph, never on the map. **Linked highlight**: gold at 18 % behind rows; map markers
not in focus step back to 28 % opacity, easing in and out over 0.18 s, and the focused ones pulse: a ring leaves
the marker and fades every 1.4 s (motion is noticed before anything else; off, with the easing, when Windows'
animation effects are off, and only while something is in focus). Losing the focus waits 0.25 s before the map
follows, so moving from one row to the next switches the highlight straight across instead of making every marker
blink. Markers on another floor than the one shown are drawn at full strength, highlighted like any other, with a
small dark disc at their upper right holding an up or down chevron (owner, 2026-10-01: half strength read as
"unimportant" and hid highlighted markers). Two or more floors away the disc becomes a small plate with the chevron
and the number of floors, counted in the map's floor list ("▲ 4": Streets has six levels, so up or down alone
didn't say how far; cartography review, 2026-10-02). Badge places are fixed: the floor at the upper right, a
cluster's count at the lower right; beside small symbols (Scav and sniper zones, bosses) the floor badge moves out
so the symbol stays visible. Spawn zone markers carry it too, by the height of their centroid.
Text says only what the place doesn't: an objective "… on Streets of Tarkov" drops the map's name on that map.

### Logo

The mark is a stencilled Cyrillic **Ш** (Shturman's initial) on a dark plate with quiet map-sheet corner marks; a
chevron cut through the centre stem points the way. The wordmark is SHTUR in ink beside MAP cut out of an amber
stencil tag: the name's two halves, navigator and map (owner, 2026-10-02, after five panels of options). The files
and their generator are in `brand/` (see `brand/README.md`); `brand\build.cs` also makes
`src/Shturmap.App/Assets/Shturmap.ico`, the exe's and the windows' icon.

- **Construction.** These rules keep the mark clear of hate, war and political signs (each option was checked
  against them). The three stems stay joined to the base and end flat at one height, with no bar across the top:
  free bars read as the Three Percenters' "III". The chevron is cut only through the centre stem and points up:
  never on all three stems (the Three Arrows), never down (a V). In the wordmark the M stays whole (a freed middle
  reads as a V), and stencil bridges go only where a closed shape needs one (A, P, R). Two faint echoes are
  accepted knowingly: the cut gives the centre stem an arrow tip (Tyr rune), and a marked centre stem between two
  others recalls the tryzub's layout.
- **Sizes.** 16 to 40 px use the plain Ш fitted to whole pixels (the cut would smudge); 48 px and up the full mark.
- **Lockup.** The gap between the icon's plate and the wordmark is a third of the cap height (owner, 2026-10-02:
  half read too loose); the plate is about 1.36× the cap height; everything is centred on one axis. The README
  shows `brand/logo-dark.svg` or `logo-light.svg` through `<picture>`, 40 px tall.
- **Colours.** The palette above. On light backgrounds ink is `#1E1F1B` and amber `#8C7436`; the icon keeps its
  dark plate on both.
- The lettering is drawn from scratch as paths, never from font outlines (§3).

### Screen anatomy

- **Status bar** (top): mode (PvE/PvP/Seasonal), raid state, last fix, as uppercase words; on the right the inputs
  (logs, screenshots, data), each with a small square light that turns gold when something needs attention, and
  the help button.
- **Rail** (left, 380 px), content by state:
  - *Plan*: last raid in one line; **Next raid**: up to four maps ranked by what can be done there, each with one
    line in words ("Complete 7 quests · progress 2 more"), the best one expanded with COMPLETE, PROGRESS and
    BRING (keys, items to bring). Each quest row there is the quest's name, then a quiet line of what it asks on
    that map in a few words, at most two lines and then "…" ("Ballet Lover" over "Find balletmeister's apartment ·
    Survive and extract"; see §5, "Quest synopsis"). The raid card keeps names only: its objective lines already
    say what to do. Clicking another map expands it and shows it on the map; that click is optional.
    Folded cards carry enough to compare without opening them (the study log: ten card clicks in 4.5 minutes to
    compare maps): one quest-type glyph per quest (gold to complete, muted to progress) and up to five cells of
    what to bring. Resting on a folded card for 0.6 s **previews** its map with its quests on it, labelled
    "PREVIEW · CUSTOMS · CLICK THE CARD TO PLAN IT"; moving to the next card switches at once, leaving puts the
    shown map back exactly as it was (pan and zoom), and a click keeps it. A raid loading ends a preview.
  - *Raid*: **the map's Plan card, live** (owner, 2026-10-01: the raid view must not be a different screen with a
    different logic). THIS RAID holds one card in the expanded Plan card's look: the map's name, the same summary
    line ("Complete 5 quests · progress 1 more"), the raid line (time left, time of day, bosses with spawn chance)
    and, when the distances aren't from a fresh screenshot, where they are from. While the raid loads, one quiet
    line under the raid line names the last loading step the game's log reported ("LOADING · MAP", then "MAP
    LOADED", "RAID PREPARED", "RAID CREATED", "PLAYER SPAWNED", "GAME POOLED", "GAME RUNNING"), over six thin
    segments, one per step, each gold only once its own step is in the log (owner, 2026-10-02: loading takes
    60–130 s and showed nothing; and nothing estimated, only what the log says, so a skipped step stays dark and no
    rule creeps by typical times). No time estimate. Then **the glance**, two rows in
    big type between hairlines: NEXT, the nearest objective with a place (its text, its quest, the distance and
    direction), and EXIT, the nearest extract or transit for your side (and what it takes); in a raid the app gets
    glances, median 3.9 s in the study log, and these are what a glance is for. Then COMPLETE, PROGRESS and BRING
    as in Plan, except that each quest line carries its objectives on this map under it: text, the key or item it
    needs (gold), and on the right the distance, direction and floor hint; "anywhere" for kills and finds with no
    fixed place, "after the raid" (muted) for hand-overs. Objectives inside a quest go nearest first, and quests
    by their nearest objective, so the top of the list is still where to go next. Below the card, extracts and
    transits for your side, each with what it takes to leave there (see "Extract requirements"). ANY MAP closes
    the rail in both states.
  - *Scav raid* (owner, 2026-10-01: a Scav needs a different view): the same card, with SCAV beside the map's
    name (PMC in a PMC raid; nothing when the logs can't tell). Quest objectives only count for the PMC, but items
    found in raid count whoever found them, so the card's summary is "Find items for 4 quests" and its one
    section is FIND IN RAID: the items your active quests need found in raid (find/hand-over objectives marked
    found-in-raid), as BRING rows with icon, count and quests, the ones lying loose on this map first ("Loose
    here · 3 spots"; pointing at a row draws the spots), at most eight and one line for the rest. One note line
    says why. The raid line drops "min left" (a Scav joins under way; the logs don't say how long is left). The
    map draws no quest objectives, and in any raid only your side's extracts. ANY MAP is hidden (a Scav's kills
    don't count). When the raid starts as a Scav, a notice replaces the PMC bring-list said at loading.
    How the side is known: the menu loads the PMC profile; a server-hosted raid's match-setup line names the
    joining profile (same id: PMC, another: Scav); a raid that starts without "GameStarting" is a Scav joining
    under way. PvE raids can be either: server-hosted ones log the match setup like PvP (seen on 25, 26 and 30
    September), locally hosted ones (all four on 1 October, two on 26 September) log neither and start with a
    zero-length "GameStarting". Those stay unknown and get the PMC view; the side tag is then a switch (PMC ⇄
    SCAV) that holds for the raid. No local Scav raid has been seen in a log yet; if one shows a line that tells,
    it replaces the switch.
- **Map** (rest): artwork, quest markers with type glyphs, extracts, transits, spawn zones (see "Map drawing"),
  player, trail, guide line to the kept quest's nearest marker. Map controls bottom-right, with the floor picker above them on maps with floors;
  one-line notices top-centre; bottom-left a WIKI MAP link (the map's interactive map on the EFT wiki, for loot,
  containers and the rest Shturmap doesn't draw) above the attribution.
- **Help** (F1 or `?`): one panel with how it works, the shortcuts, the quest-type legend and the map symbols (see
  "Map drawing", "Legend"). Opens
  once by itself on first run.

### Map drawing

Rules from a cartography review of the map (2026-10-02; the owner approved its twelve changes, with the change to
spawns below).

- **Four levels, the rule for every symbol.** A reader takes in the map in order of importance; each level differs
  from the next in size, contrast or colour, and there are few things at the top.

  | level | members | treatment |
  | --- | --- | --- |
  | 1 | the player; the kept quest, its guide line and plate | own hues (sand, cyan), rings, drawn last, never faded |
  | 2 | quest objectives; extracts and transits for your side | amber discs with glyphs (10 px); 15 px triangles and diamonds; labels in ink |
  | 3 | boss, sniper and Scav zones | one marker per zone; Scav zones unlabelled |
  | 4 | the artwork, its names, the sheet grid | receded; names thinned with zoom |

  Extracts and transits are as large as the boss diamond (15 px across), no longer smaller. A new symbol gets a
  level, a shape no other symbol uses, a colour from the palette (no new hues) and the collar.

- **Spawns: one marker per zone, at its centroid** (owner, 2026-10-02: the player needs to know which area has
  Scavs, where the bosses and the snipers are, not each spawn point; "just use the centroid, do not colour code the
  potential spawn area"). tarkov.dev names a zone for each spawn point; the points of one zone get one marker at
  their centroid, the mean of X, Y and Z. A marker must stand among its points, within 25 m across and 3 m (about a
  floor) in height of one of them: where a zone's centroid doesn't, the zone is cut in two at its widest gap, until
  every group's does, and each group gets its marker (owner, 2026-10-02: on Customs and Interchange some zones spread
  over 300–450 m and their centroid landed 40–250 m from the nearest point; Customs' sniper marker stood on the
  ground between its two points, 228 m apart on the 2nd and 4th floors). Split only where needed, Woods keeps 28
  Scav markers for its 11 zones and Customs 24 (PvE). `shturmap-cli spawns [mode]` lists, per map, how far each marker stands from a real
  point. No areas are drawn. AI Scav zones (side "scav", category "bot" or "all", not "sniper") are small quiet rings in
  ink, unlabelled and not hoverable. Sniper zones (side "scav", categories "bot" and "sniper"; Ground Zero's
  player spawns tagged "sniper" don't count) are a hollow ink hexagon labelled "Sniper": a shape no other symbol
  uses, not a reticle, which is the Elimination glyph. Bosses (mobs "boss…") get one red diamond per spawn zone,
  labelled with the two numbers the data gives, the boss's chance on the map and, for a boss with several zones,
  that zone's share: "Kollontay 75% · 50% here", "Kaban 75%"; never their product, since the data doesn't say the
  share is conditional. A boss zone split in groups says this once, on its largest group; the others are bare
  diamonds that light with it. Bosses whose groups have the same centroid share one marker ("Reshala 75% · 33% here /
  Knight 25%" on Customs' Stronghold), and pointing at a boss lights all its zones.
- **Labels by priority** (`MapRenderer.Layout`). Every symbol is placed before any text, so no label covers a
  symbol. Then labels in this order: the kept or pointed-at quest, bosses, quests, extracts and transits,
  snipers, and the map's own names last, largest tarkov.dev size first. A marker label tries right, left, above
  and below its symbol, in that order, and is dropped when all four are taken (pointing at the marker still names
  it); a selected label that finds no free place goes on the right anyway. A name is said once per neighbourhood
  (250 px): eight "Abandoned Cargo" places in one block carry one label. Map names are boxed by their rotated
  extent and drawn on the marker labels' halo (the ground at 86 %, 3 px): without it Ink at 59 % measured 1.4:1 on
  Streets' light streets, with it 4.7:1.
- **Map names by tarkov.dev's label size.** Every name was 11 px, though tarkov.dev sizes them (Streets: 90 for
  landmarks such as Kilmov Shopping Mall, 80 for streets, 70 for shops, 60 for cafés; Interchange's shops are 65).
  Landmarks (90 and up) are set in 12 px semi-bold caps, letter-spaced; streets (80, and names without a size) as
  before, 11 px, at every zoom; 65–70 from 1.5 times the zoom that shows the whole map; 60 from 2.5 times. Larger
  names are placed first. Names out of view aren't placed.
- **Distances on the map.** The guide line to the kept quest carries the card's number on a small dark plate with
  a cyan hairline at the middle of its part in view ("69 m", the same horizontal distance and rounding as the
  card, `MapRenderer.DistanceText`), with the fix's age once it is a minute old ("69 m · 4 MIN": the distance is as
  old as the position). A line too short to carry it clear of its ends has none. A scale bar at the lower left,
  above the wiki link and the credit line, takes the longest round length (1, 2, 5, 10, 25, 50, 100, 200, 500 m …)
  that fits in 120 px and follows the zoom. No range rings (three circles on every view for a question the bar and
  the plate answer) and no north arrow (the artwork's orientation isn't verified; the cards give directions
  relative to the facing).
- **The player at full strength.** The sand disc no longer fades with age (it sank to 45 % after 3.5 minutes,
  below the quest markers; transparency reads as "less important", the reason half-strength other-floor markers
  were rejected). It has the kept quest's vocabulary instead: a steady ring, sand on a dark band, at 12 px. Once
  the position is a minute old the ring turns dashed and a small dark tag beside it gives the age in whole units,
  as the top bar does ("4 MIN", "2 H"). The facing cone (first minute only) has its arrow outside the ring.
- **One meaning per colour, shape as a second cue.** Simulated colour blindness (Machado 2009) put the quest amber,
  the PMC-extract green and the shared-extract khaki within ΔE 4–7 of each other. Shared extracts are split down
  the middle by a dark line (two sides, one exit), not hollow (hollow means "one of these places"). Done
  objectives are a smaller muted-ink disc with a check mark, no longer translucent green, so green means extracts
  only; their zones turn muted too. The trail is the player's sand, no longer the Scav-extract teal.
- **Clusters of one objective's places.** Places of one objective (and one kind) whose markers would overlap,
  closer than two marker widths on screen, merge into one marker at the group's medoid (a real place, so the
  marker never stands where nothing is) with a count badge at its lower right in the marker's colour: "Following the
  Bread Crumbs" lists 16 places on The Lab, which piled up with four labels. Groups split as the view zooms in;
  places of different objectives never merge; possible places stay hollow; the kept or pointed-at quest clusters
  too. A group with any place on another floor shows the floor arrow. The largest group carries the label;
  pointing at a cluster points at its quest.
- **Places out of view.** While a quest is kept highlighted, or pointed at, its places outside the view are shown
  as small chevrons 18 px in from the edge, toward them from the middle of the view, one per direction (places
  whose edge points lie within 56 px merge) with how many lie that way, in the quest's colour (cyan when kept,
  gold when pointed at). Done objectives don't count. Same vocabulary as the player's edge badge, smaller and
  without a plate: the player is level 1. Nothing is drawn when nothing is highlighted.
- **Legend.** The help panel's ON THE MAP rows are drawn by `MapRenderer` itself (`MapLegend`: one small bitmap
  per symbol, made with the map's own drawing code at twice the DIP size), so they can't drift from the map; the
  hand-drawn XAML shapes showed a plain disc for the quest marker, which has a collar and a glyph. Rows go by the
  four levels, every symbol on the map has one (the done objective, quest zones, the floor arrow, the guide line
  and its plate, the trail, the edge badge and chevrons, clusters and the sheet were missing), and each names
  shape and colour, not colour alone. A new symbol gets a `LegendSymbol` and a row in the same change.

### Quest cards

One card per quest, the same everywhere: trader portrait, type glyph and name; trader, level and Kappa /
Lightkeeper; the state and where it came from ("Active · from the game log, 25 Sep"); every objective with its
glyph, where it is, the item it is about and, in a raid on its map, how far and which way it is from the last fix
("69 m · ahead-left"); BRING (keys and items, with icons and maps); UNLOCKS (the quests it opens); a link to the
wiki page. Nothing else: no rewards, no guides.

The **item card** (a key or an item): its icon and name; GET IT, easiest first: traders that sell it (loyalty level,
price, quest unlock), barters, hideout crafts, the flea market (from which level), and where it lies loose ("Loose
on Customs · 3 spots"); then which of your active quests need it and how ("Key · Customs", "Bring ×3, to plant ·
Streets of Tarkov"). While the pointer is on an item anywhere, its loose spots on the shown map are drawn as small
open squares. Every BRING row (Plan, the raid card, a quest card) says what the item is for and for which quests
("to mark, for Revision", "to wear, for Dandies", "key for Ballet Lover") and has one line with the easiest
source; the tiny need cells beside quest names say the same in their tooltips (the study log: gear item cards were
opened 10 to 21 times each to find out why and where). Gear a kill objective asks to be worn (tarkov.dev's
`wearing`: sets worn together, any set will do) is a requirement of its own, "to wear", shown as "Bomber beanie /
RayBench Hipster Reserve sunglasses" (neutral about and/or: the data's sets don't always match the quest's
wording, which the objective text gives anyway); item cards list it as "Wear, for kills".

Cards behave like the nested tooltips in Crusader Kings III:

1. **Hover**: rest the pointer on a quest, key or item for 0.65 s on a rail row or map marker (the study log: 62 %
   of cards opened from lists closed within a second, opened by a pointer passing over), 0.4 s on a row on a card
   or in a pinned window, and
   its card appears beside it, see-through (80 %) so it doesn't hide the map. It stays while the pointer is on its
   subject or on the card, and goes 0.35 s after it leaves both. Moving down a list swaps it after 0.12 s.
2. **Held**: a click holds it: on the quest (row or marker), or anywhere on the card. A held card is solid with an
   amber border and stays while the pointer is near it. It closes on a click on nothing in particular (bare rail
   or map), Esc, another click on its quest, a full 0.4 s rest on something else that opens a card in its place,
   or the pointer moving more than 240 px away from it. Nothing holds by itself (owner, 2026-10-01: the timed hold
   was dropped).
3. **Nested**: on a card, rest on a key, an item or a quest (UNLOCKS, NEEDED FOR) and that one's card opens beside
   it, and so on; a click holds it too. Moving back to an earlier card closes the unheld later ones.
4. **Pinned**: the pin turns a quest card into a small window with a normal title bar, to move anywhere and leave
   open; its own nested cards open beside it. The card the pin was on closes, with anything opened from it, so the
   quest isn't shown twice (owner, 2026-10-01). In a raid it shows live distances, which is what a pin is for: a
   tracker for the quests you chose, readable without the mouse. Pinned cards come back after a restart and close
   by themselves when their quest is completed. A pinned window's title bar is kept inside a screen's work area,
   when it comes back and 0.6 s after it was moved, by the least move that does it (the study log: one closed
   twice with its title bar above the screen, where it can't be grabbed).

Rows on a card take part in linked highlighting (pointing at a key lights it up in BRING and the quest on the map),
but don't light up for their own card's quest, or the whole card would glow.

### Keeping a quest highlighted

Pointing highlights for as long as the pointer stays; the quest's highlighter (on its rail rows and its card)
**keeps** it highlighted, so its markers are easy to find on the map while you look away (owner, 2026-10-01). A
click on the quest itself only keeps its card open. The
kept quest has its own colour, cyan (`KeptBrush`, `MapRenderer.Kept`): owner, 2026-10-01, gold among gold didn't
stand out; cyan is the one hue nothing else on the map uses, the artwork included, and stays apart from gold with
any colour vision. Its rows keep a cyan tint; on the map its markers turn cyan and grow (14 px radius, against 12 for
what is pointed at and 10 at rest) inside a steady cyan ring on a dark band, its zones turn cyan, the rest stay
dimmed, and a dashed cyan line runs from your last fix to its nearest marker. It pulses three times when
kept, and again when the pointer comes back from something else, then holds still: a marker pulsing all raid would be
motion at the edge of the player's eye. Pointing at something else shows that instead, for as long as the pointer is
on it. One quest is kept at a time; another click on it, a click on another quest, or Esc (after the cards) lets it
go, and it goes by itself when the quest is done. A highlight with nothing on the shown map (a quest kept from
another map, a quest for any map) dims nothing.

### Extract requirements

Shown under each extract in the Raid rail, short, in gold, with the item's icon when one is handed over:

| from | example |
| --- | --- |
| `transferItem` in roubles, dollars or euros | "Pay 5,000 ₽" (car V-Ex), "Pay €2,400" (Icebreaker heli) |
| `transferItem`, any other item | "Hand over Note with code word Onyx" (secret extracts) |
| internal name `Alpinist*` / `RedRebel*` | "Red Rebel ice pick and paracord, no armored rig" (Cliff Descent, Mountain Pass, Climber's Trail) |
| "(Flare)" in the name, or `sniper` in the internal name | "Fire a red signal flare there" |
| "(Co-op)" in the name | "Co-op: a PMC and a player Scav leave together" |
| `switches` | "ZB-013 Power Switch first"; a switch tarkov.dev lists on most of a map's extracts is only shown where its name contains the extract's name (it lists the ZB-013 switch on every Customs extract) |
| transit `conditions` | "TerraGroup Labs access keycard required (1)" |

Internal extract names are kept from the payload before translation (`GameData.ExtractKeys`); transit conditions
are translated although the payload's translation list misses them.

### Keyboard (window focused only)

| key | action |
| --- | --- |
| F | show my position (the view never moves by itself) |
| + / − | zoom in / out |
| 0 | show the whole map |
| PgUp / PgDn | show the floor above / below |
| Esc | close the cards, else stop keeping the quest highlighted |
| F1 or ? | help |

Keyboard accelerators sit on the window root with their placement hidden; WinUI would otherwise show the first
one's key as a tooltip over the whole window.

Mouse: drag to pan, wheel to zoom at the cursor, double-click to zoom in, point at anything to see what belongs to
it, click a quest (in the list or on the map) to keep its card open, click its highlighter to keep it lit, click the
edge badge to show your position.

## 5. Quest taxonomy

Every objective gets one type, from tarkov.dev's objective `type`. The game gives each quest one hand-assigned
type on its Tasks screen (Elimination, Pickup, Exploration, Discovery, Completion, …); that type is not in any
public data and can't be derived reliably (a check against the wiki matched 62.5 %), so Shturmap types objectives
instead and borrows the game's names and look where one fits, so the labels feel familiar:

| type | glyph | objective types | in raid? |
| --- | --- | --- | --- |
| **Elimination** | crosshair (custom; the game uses a skull) | `shoot` | yes |
| **Exploration** | magnifier `E721` (as in the game) | `visit` | yes, at a place |
| **Pickup** | pointing hand `E7C9` (as in the game) | `findQuestItem` | yes, at a place |
| **Place** | pin `E840` | `plantItem`, `plantQuestItem`, `mark`, `useItem` | yes, at a place; needs an item |
| **Find in raid** | bag `E719` | `findItem` | yes, anywhere (FIR) |
| **Survive** | runner `E726` | `extract`, `experience` (an in-raid health condition, not XP) | yes |
| **Trader** | people `E716` | `giveItem`, `giveQuestItem`, `sellItem`, `buildWeapon`, `traderLevel`, `traderStanding`, `skill`, `taskStatus`, `dialogue`, `globalVariable`, anything new | no |

A quest's type is that of its most common in-raid objective type (ties: Elimination, Pickup, Place, Exploration,
Survive, Find in raid); a quest with no in-raid objectives is a Trader quest. This is derived, not the game's own
label. Requirements use a key glyph `E8D7` for keys and a briefcase `E821` for items to bring. Never use the
game's icon artwork; the glyphs only echo it.

### Quest synopsis

Under each quest's name in Plan's COMPLETE and PROGRESS, one quiet line says what the quest asks on that map:
"Get valuable item", "Kill Scavs ×10 with M4A1, M16, ADAR, or TX-15", "Mark first LAV III, Stryker, second LAV
III" (owner, 2026-10-02). Why: before a raid the owner pointed at every quest row to see what it wanted (the study
log: 210 quest cards opened from the rail in the menus over two evenings, 188 of them in back-to-back scans, and 53
looks at 12 quests in one 12-minute stretch). The owner chose the name first and the synopsis under it over the
synopsis in the name's place: the name is what the trader, the wiki, the BRING rows ("for Dandies") and the map's
labels use, so it stays the thing you recognise.

- **Made from the loaded data, without AI** (owner: it must keep up with tarkov.dev's quest updates without help).
  `QuestSynopsis` (Core) works on tarkov.dev's objective texts and count fields for the objectives the planner
  counts on that map (in-raid, not optional; hand-overs are left out). Nothing is stored: a new quest gets its line
  as soon as the data has it.
- **It only subtracts.** It may (1) replace a known verb phrase at the start from a fixed table ("Locate and
  obtain" → Get, "Locate and neutralize" and "Eliminate" → Kill, "Locate" → Find, "Find the item in raid:" → FIR,
  "Survive and extract through" → Extract through, "Use the transit to" → Transit to, and the term "PMC operatives"
  → PMCs); (2) remove known filler: lowercase articles (not after "of"), this map's name and lists that hold it
  ("on Woods, Ground Zero, or Customs"), "with an MS2000 Marker" (it is in the row's bring cells), "at the
  specified spot", "from the location", and the place of an objective whose marker is on this map ("in dorm room
  203"), together with a word leading into it ("hidden"); (3) add the count field as "×N", before a kill's
  conditions or at the end, as the game and the BRING rows show counts. Nothing else. A reworded text then gives a
  longer line, never a wrong one; a text with no known verb is shown as written, less the map's name.
- **Never dropped**: conditions and exclusions, word for word (brackets, "excluding", "without", "only", "while",
  "using", "wearing", "during", "in one raid", headshots). A place that holds one stays whole. A number the text
  gives that the count field contradicts is left out, and so is the count (Job for a Patriot says 20 PMCs, its
  count 10). A place stays when cutting it would leave one word ("Stash package at laboratory storage room") or
  when two objectives would then read the same (Spotter's two sniping positions).
- **Merging**: consecutive objectives with the same verb share it, and words all their objects share are said once
  ("Stash AK-50 body, handguard, barrel"; "Kill Knight, Big Pipe, Birdeye (in one raid)"), never across a count and
  a shared ending only after short names ("first, second, third yellow minibus").
- **English only.** The rules are written for English texts; in another game language the row shows the name alone.
- **Checks.** `QuestSynopsis.Problems` checks a line: every word is in the objective's text or the table, or is
  its count; every condition of the text is kept; nothing stops mid-phrase. A test runs it over every quest on
  every map in the local tarkov.dev cache (PvE and PvP; skipped without a cache, since the quest texts are
  tarkov.dev's and BSG's and stay out of the repository). On 2026-10-02: 614 PvE and 620 PvP rows, no breaks, no
  fallbacks; a quarter run past two lines and end in "…" (long item lists, kill conditions).
- **After a tarkov.dev or game update** run `shturmap-cli synopses pve` and `… regular`: they list every Plan row
  with its line and flag FALLBACK (a text with no known verb), LONG (more than two lines at the row's width) and
  BREAK (a rule broken). Add a verb or filler phrase to the table only when it subtracts.

## 6. Raid requirements

- **Keys**: the quest's `neededKeys` for that map, and each objective's `requiredKeys` (a list of alternatives:
  shown as "A or B").
- **Bring**: items a Place objective consumes (`items` of `plantItem`, `markerItem` of `mark`, `useAny` of
  `useItem`, the quest item of `plantQuestItem`), with counts summed per item.
- Shown aggregated per map in Plan, and inline on the objective in Raid ("needs Dorm room 114 key").
- Shturmap cannot see the stash; it lists what is needed, not what is missing.

## 7. Raid planner

For each map (variants sharing artwork, like Ground Zero 21+, count as one), using active quests only
(objective progress is unknown to Shturmap):

- An objective is **doable** on a map if it names that map or has a place there, or names no map and is Kill,
  Collect or Survive. It is **tied** to the map if it names the map or has a place there.
- A quest counts for a map only if at least one of its doable objectives is tied to it; work that fits any map
  doesn't argue for one map over another.
- A quest can be **finished** on a map if all its in-raid objectives are doable there, none is Collect (found-in-
  raid items depend on luck) and no kill count is above 3; otherwise it is **progressed**.
- Score = 3 per finishable quest + 1 per tied objective with a place + 0.6 per tied objective without one + 0.1 per
  untied doable objective. Maps are ranked by score; the top four are shown, the best (or the one on screen)
  expanded.
- Quests whose in-raid work fits any map are listed once under **Any map**.
- Walking estimate: a nearest-neighbour route through one place per located objective, at 3 m/s, shown against
  the raid length ("~7 min walking · 35 min raid").
- Quest-level keys are shown on the objectives that have a place on that map, not on hand-ins or extracts.

## 8. Engineering

| project | role |
| --- | --- |
| `Shturmap.Core` | no Windows dependencies: screenshots, projection, floors, bearings, logs, raid tracker, quest progress, taxonomy, planner, name matching |
| `Shturmap.Game` | install discovery (BSG launcher and Steam are equal), log tailer, screenshot watcher, game settings |
| `Shturmap.Data` | json.tarkov.dev loader (ETag cache, translations), SQLite progress store, game art cache (portraits, icons) |
| `Shturmap.Map` | SkiaSharp drawing: artwork per floor, camera, renderer, map content, glyphs |
| `Shturmap.Session` | the coordinator: inputs in, one immutable `SessionSnapshot` out |
| `Shturmap.App` | WinUI 3 window; reads snapshots, never game files |
| `tools/Shturmap.Cli` | headless runner: locate, replay, data, ocr, render, watch, simulate |

Rules:

- Core logic is pure and unit-tested; fixtures come from real logs (scrubbed with `tools/make-log-fixtures.ps1`).
- Every change keeps `.\eng\dotnet.ps1 test --solution Shturmap.slnx` green, including `SafetyTests`.
- Verify UI with `Shturmap.exe --snapshot <folder>` (renders the window and the map to PNGs) or
  `shturmap-cli render`; never capture the user's screens. For website media, `tools\fake-raid.ps1 -Window
  1600x900 -Scale 2` renders at a fixed size (the app's `--window`; the UI reads larger), in English
  (`--culture`) and at twice the pixel density (`--snapshot-scale`, sharp on high-DPI screens). `-GroupPick`
  plays a group's map pick in the menus (with `-PlanOnly`); `-HoldLoading` stops the raid halfway through loading.
- The website's hero clip comes from a demo mode, `--demo <quest>` (fake games only; `src/Shturmap.App/Demo.cs`).
  It must never read as live tracking (owner, 2026-10-02): the position changes exactly once, and visibly after a
  drawn press of the screenshot key. The key comes in a pause: the window dims, the key stands large in the middle
  with "SCREENSHOT · POSITION FROM THE FILE NAME", it is pressed, the file name appears, and the new position is
  held back from the view (demo only) until the dim clears, so the marker moves after the press is seen. Then a
  drawn pointer keeps the quest highlighted, the map zooms to it, its objective's card stays up to be read, and the
  view goes back. It calls the app's own code; no input reaches the system. `tools\fake-raid.ps1 -Demo` writes the
  one screenshot after the press and records the clip with `tools\record-window`, which records Shturmap's own
  window only (Windows.Graphics.Capture, no cursor; outside `src/`, so `SafetyTests` still keeps capture APIs out
  of the app). Never record a monitor, the desktop or another window (owner, 2026-10-02: window capture of Shturmap
  only).
- Website media is regenerated by `tools/make-media.ps1` in the shturmap.github.io repository; see its CLAUDE.md
  and README.
- Comments explain why, not what. Match the surrounding style.
- Write user-facing text plainly: short sentences, units on numbers, no jargon.

### Distribution

The release is **one file**, `Shturmap-<version>-win-x64.exe` (owner, 2026-10-03: easiest to pass on; nothing to
unzip, so nobody runs it from inside a zip). `eng\release.ps1` builds it, and the same build as a folder, zipped
beside it for whoever prefers that, each with a `.sha256`; `eng\publish.ps1` builds only the folder
(`artifacts\Shturmap`, the developer's copy and what `tools\fake-raid.ps1` runs).

- **How it runs.** .NET's single-file publish with everything inside (`IncludeAllContentForSelfExtract`,
  compressed): WinUI needs its files on disk, so the first start of each build unpacks them (about 200 MB) to
  `%TEMP%\.net\<exe name>\<bundle id>` and runs from there; later starts reuse that copy. Measured 2026-10-03:
  86 MB to download; a session is ready 1.3 s after the first start and 0.44 s after later ones. The single exe is
  not precompiled (ReadyToRun): as one file that builds a 120 MB composite image of the whole framework, which made
  the exe 110 MB and its unpacked copy 285 MB to start 0.1 s sooner; precompiled without the composite image, it
  failed to start. The folder build stays precompiled.
- **Its name doesn't matter.** WinUI looks for the app's resources (its compiled XAML) in `resources.pri` or
  `<exe name>.pri`, so the project names its PRI file `resources.pri`: named after the project, any other exe
  name (the release's own, or a browser's "Shturmap (1).exe") made the window fail to load.
- **No pile of copies.** Each build and each exe name leaves its own unpacked copy. A start from one removes the
  others in the background (`UnpackedCopies`, logged): only `%TEMP%\.net\Shturmap…\<id>` folders holding
  `Shturmap.dll`, never its own, and never one in use: a copy is moved aside before it is deleted, which Windows
  refuses while another Shturmap runs from it. With `DOTNET_BUNDLE_EXTRACT_BASE_DIR` set elsewhere it does nothing.
- **Nothing beside the exe.** The unpacked folder is a cache: the app writes nothing there and keeps all its data
  in `%LOCALAPPDATA%\Shturmap`. The "build time" in the study log is the exe's own file time, not the unpacking's.
- **Unsigned.** Windows SmartScreen asks once ("More info → Run anyway"); the README says so.

### How the parts work

**Finding the game.** Candidates come from the `EscapeFromTarkov` uninstall key (HKLM 32/64, HKCU), the
launcher's `gamesRootDir` (older `gameRootDir`) and its subfolders, `C:\Battlestate Games`, and Steam's
`libraryfolders.vdf` + `appmanifest_3932890.acf`. A candidate needs `EscapeFromTarkov.exe` or a `Logs\log_*`
session (also under `build`). The newest log session wins; all valid installs feed the quest backfill.

**Screenshot names.** `yyyy-MM-dd[HH-mm]_x, y, z_qx, qy, qz, qw_clock (n).png`; the trailing number is the in-raid
time of day when a position is present. Facing: `yaw = atan2(2(qx·qz + qw·qy), 1 − 2(qx² + qy²))`.

**Projection.** tarkov.dev's math: rotate (x, z) by `coordinateRotation`, `X = a·rx + b`, `Y = −c·ry + d`, SVG
fitted into the projected `svgBounds ?? bounds`. Golden test: a Streets screenshot lands at
(232.2, 749.0) in `StreetsOfTarkov.svg`.

**Logs and raids.** Menu → Loading (`scene preset path:`) → InRaid (`GameStarted`) → Menu
(`PrepareSelectedProfileLocally` or matching cancelled). Map: scene path ↔ `scenePath`, then `Location:` ↔
`nameId`, then an alias table. Side: the match-setup `Profileid` equals the menu profile for PMC raids and differs
for Scav raids; locally hosted PvE raids stay Unknown. Quests: `ChatMessageReceived` type 10/11/12. Loading steps:
`LocationLoaded`, `GamePrepared`, `GameCreated`, `PlayerSpawnEvent`, `GamePooled`, `GameRunned` (and
`MatchingCompleted` for the study log). Group: `GroupMatchRaidSettings` (its `raidSettings.location` and
`timeVariant`); `GroupMatchRaidReady`, `GroupMatchRaidNotReady` and `GroupMatchStartGame` by their header alone,
because their bodies (and the invites') hold other players' profiles, which Shturmap never reads. The insurer:
`ChatMessageReceived` type 2 (a trader message) with `systemData.location` when insured gear was lost, type 8 (the
insurance return) with the gear hours later. Each notification is logged twice ("Got notification" with its body,
then "Received notification"); only the first counts.

**Quest progress.** Only the game's log counts: quest started/failed/completed notifications, backfilled from every
log session on disk and followed live; newest wins. Prerequisites of active or completed quests that strictly
require "complete" are shown as implied, never stored. A quest started before the oldest log on disk is not known.
Databases from earlier versions may hold Tasks-scan and TarkovEyes-import rows; they are ignored (and could never
reopen a quest the log saw completed). `shturmap-cli quests` lists active quests with every observation.
`shturmap-cli synopses [mode]` lists every Plan row's synopsis with its flags (§5, "Quest synopsis").

**Floors.** The floor shown is the player's, from the height of the last fix. The picker lists floors that have
artwork of their own, top first, with a dot on the player's; a pick (click or PgUp/PgDn) holds until the next
screenshot. Floors without their own artwork (Customs' 4th, Reserve's upper floors) are drawn in the base layer.
On a sheet (maps without artwork) the floors are tarkov.dev's tile layers. Map labels with heights (tarkov.dev's
bottom/top) show only on their floor, as on tarkov.dev; labels without heights show on every floor.

**Item sources.** json.tarkov.dev `items` (17 MB; only trader offers, flea level and a last price are read),
`barters`, `crafts`, `hideout` (+ translations), fetched after the main data and refreshed daily; loose spawns come
from the maps payload's `lootLoose`.

**Language.** Texts come in the game's language (its settings' `Language`), translated by tarkov.dev's
`<payload>_<language>` files. The game names some languages its own way, and tarkov.dev answers those with 404:
`GameDataLoader.ApiLanguage` maps `ge`→`de`, `cz`→`cs`, `jp`→`ja`, `kr`→`ko`, `po`→`pt`, `tu`→`tr`, `ch`→`zh`,
`es-mx`→`es` (2026-10-02: a friend's German game asked for `maps_ge`, and no data loaded at all). A language
tarkov.dev still lacks falls back to English for everything, so the data stays one language and always loads.

**Study log.** `%LOCALAPPDATA%\Shturmap\study\yyyy-MM-dd.jsonl`, one JSON object per line: `t`, `src` (`game` or
`ui`), `ev`, event fields, and `ctx.*` (raid phase, map, raid minutes, age of the last fix) on every line, so UI
use can be lined up with raids and quest completions later. Game: app start/exit, data loaded (with the top
suggestion), mode, raid loading (with the suggestion rank of the map actually played and the bring list), raid
start/end, quest started/completed/failed (live only), each fix, each notice. UI: window focus/blur and pointer
in/out (with durations: the closest signals to attention), map pan (one per drag), zoom (one per wheel burst,
button or key), fit, follow, floor picks, map picks (picker or plan card), selections, hovers resting ≥ 0.4 s
(where: list, card, pinned, map), card open/hold/close (level, seconds open), pins and pinned-window closes,
help open/close, keys, rail scrolls, notice dismissals. Added after the first study (2026-10-01): the plan's
COMPLETE and PROGRESS quest ids at raid load (to check which got completed), the evidence for each side decision,
screenshots that gave no position, whether a new position was in view and uses of the edge arrow and F, map
previews, pinned-window moves with their final position and whether they were clamped, notices expiring vs
closed, the quests visible in the rail and the rail's scroll position when the window gets focus, stale-position
banners seen, side switches, why a session started (the previous one ended cleanly or not, the build's time), and
active-quest count changes outside quest events. Added 2026-10-02: group picks (location, map, time variant) and
group ready / not ready / start; each raid's loading steps with their seconds since the scene line (in
`raid.start`); the raid clock in each fix; the insurer's notes (kind, location, item count). How a raid ended
(survived, killed) is in none of the allowed logs. The nearest thing is `raid.outcomeHint` `{ lostInsured: true }`:
the insurer's "lost" note came during the raid or within 5 minutes after it, on the same location (in the owner's
logs it came 17–20 s before the raid's end line). It is a hint for later studies of the plan's accuracy, never shown;
no note proves nothing, since gear may not have been insured. Only Shturmap's own windows are observed; nothing is
sent anywhere.

## 9. Status

- Done: discovery, watchers, raid tracking, map with floors, player, facing, trail, extracts, transits, quest
  markers, objectives by distance, log backfill, live quest events, safety test,
  self-contained publish, taxonomy, raid planner, requirements, raid line, help panel, keyboard shortcuts,
  occasional-position UX (fix age, fading marker, compass directions), bring-list notice on raid load, linked
  highlighting, quest cards (hover, held, nested, pinned with live distances), item cards with sources and loose
  spots, trader portraits and item icons, floor picker, study log, Tarkov-style visual language.
- Next: read a study log from a real session and correlate it with quest completions (`shturmap-cli` command).
- Named **Shturmap** (owner, 2026-10-01; was Spotter): Shturman, the navigator, plus map, and a word of its own
  so a search finds the app rather than the Woods boss. The old data folder and database move over on first start.
- The Lab, Labyrinth and Icebreaker are drawn as sheets (§3, "Maps without artwork"; 2026-10-02).
- Released as one exe (2026-10-03; §8, "Distribution"): 86 MB to download, about 200 MB unpacked.
- Open: manual quest editing; objective progress; the unpacked size (budget 80–120 MB, needs trimming).
