# Shturmap design

This document is binding for anyone changing Shturmap, people and agents alike. Read it before you change
behaviour or UI, follow it, and update it in the same change when a decision changes.

## 1. What Shturmap is

Shturmap answers two questions for an Escape from Tarkov player, on a second monitor:

1. **Before a raid: where should I go, and what do I need to bring?**
2. **During a raid: where am I, and where is my next objective?**

That is the whole product. A feature belongs in Shturmap only if it helps one of these two questions and works
without the player having to click during a raid. When in doubt, leave it out. One exception, the owner's, for
eye candy at a raid's end: the raid replay (owner, 2026-10-07: "more on the eye-candy side of things like the loading
animations"; "Map drawing", *The raid replay*). And one for learning the app: the tour at a first start (owner,
2026-10-09: "a proper first-time-opening tour of the app ... Here we can be a bit more flashy with nice animations";
§4, "Screen anatomy", *The tour*). And one for a quest handed in: the QUEST COMPLETE cue (owner, 2026-10-07: "For
completed quests, also make a nice animation"; §4, principle 11).

**Not goals** (declined on purpose; don't add them): item prices or a flea-market view (a price appears only as
part of "where to get" an item a quest needs), hideout tracking, loot or container maps beyond what quests need,
item scanning by hotkey, ammo charts, squad sharing, achievements, overlays on the game window, statistics
dashboards, a settings maze.

## 2. Ground rules (game terms of service)

Shturmap reads only files the game writes for the player, plus public community data:

- screenshot **file names** in `Documents\Escape from Tarkov\Screenshots`, for positions; and, of a screenshot taken
  in a raid, **the top right corner of its picture**, for the game's extract list (below; the player can turn that
  off, and no other part of any picture is looked at);
- `application_*.log` and `push-notifications_*.log` in the game's `Logs` folder, with shared read access;
- `Control.ini` and `Game.ini`, read-only.

Its own code never opens a handle to `EscapeFromTarkov.exe`, reads or writes its memory, injects, hooks, sends input,
registers global hotkeys, captures the screen, draws over the game, edits game files (including
`Logging.config`), or reads `backend_000.log` or launcher credentials (only `gamesRootDir` is read from the
launcher settings). `SafetyTests` fails the test run if forbidden APIs appear in `src/`. Keyboard shortcuts work
only while Shturmap's own window has focus.

**The installer is the one exception, and the texts say so** (review of 2026-10-04; owner the same day: say it
precisely rather than change how updates work, for now). Velopack's `Update.exe`, which installs, updates and
removes Shturmap (§8, "Distribution"), closes running copies of Shturmap first. For that it lists every running
process and opens each one with `PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_TERMINATE`, reads the path of its
executable, and ends only those under Shturmap's install folder (its `get_processes_running_in_directory`; checked
in the installed 1.2.161 binary's imports and in Velopack's source). So while it runs (the Setup, an update being
applied at Shturmap's start or on RESTART NOW, an uninstall) a running game is asked for such a handle like every
other process; nothing is read from it or written to it. `SafetyTests` reads `src/` only and can't see this. The
README says it under "The installer and running programs", with the advice to install and update while the game is
closed; help says only what holds for Shturmap's own code. Still open: applying updates in a way that looks at no
other process (an apply step of our own, or applying only on the player's word).

**One corner of a screenshot's picture is read, for the extract list** (owner, 2026-10-05: "The currently open exits
are displayed on the upper right corner of a screenshot ... It would be great if we could check a screenshot if it's
taken if it contains this information ... read it and update the map accordingly"). Until then no picture was ever
opened: positions come from the names, and the Tasks-screen reading of September was removed on 2026-10-01. The game
gives each raid its own extracts, by where the player starts, and neither its logs nor tarkov.dev's data say which;
it shows them in a list at the top right, when a raid starts and when the player asks for it (O twice by default).
That list is the one thing read from a picture, and the reading is kept this narrow (`ExitListReader`,
`Shturmap.Core.Screenshots.ExitList`):

- **Which pictures:** only a screenshot the watcher reported as new, named with a position, while the log shows a raid
  running on a map the data knows. Never a menu screenshot, never one already in the folder, never while a raid loads.
- **How much of it:** the top right corner (0.60 by 0.62 of the picture's height). If no green bar stands near its top
  (the list's header), nothing is read and the picture is let go. With the bar, the words in the corner are read with
  the text recognition built into Windows (`Windows.Media.Ocr`): on the PC, with no download, no model of Shturmap's
  own and nothing sent. On a full-size screenshot a look takes 110–180 ms, most of it decoding the PNG.
- **What is kept:** which of the raid map's extracts the rows name, and whether the game marked a row "??:??:??".
  No pixel and no other word is kept, logged or sent; the study log notes counts only (`exits.read`). It is forgotten
  when the raid ends.
- **How:** the file is opened for reading, shared with the game that may still be writing it, and only once it is
  written to its end (a PNG's IEND chunk; half a list read as the whole would call the missing rows' exits closed);
  it is tried for about three seconds, then left. Nothing is written to the screenshots folder. This is a file the
  game saved for the player, like the logs: no screen is captured (`SafetyTests` still forbids that), nothing is asked
  of the game, and a list is read only from a screenshot the player took with their own key press (the rule below,
  "never take a screenshot for the player", stands).
- **The player's say:** "Read the extract list from screenshots" in settings, on unless unticked (a setting that had to
  be found first would leave EXIT guessing for most players; principle 1). Unticked, no picture is opened. Its note,
  help, the README, PRIVACY.md and the website say what is looked at; the diagnostics carry the setting's state and
  the recognition's language.

This widens what Shturmap takes from the game's files by one thing, on the owner's word; BSG's 4.3.4 (below) speaks of
"information reproduced ... by the Game" either way, and the texts that named the risk still do. Nothing else of a
picture may be read without the owner deciding it anew: not the raid timer beside the list (Windows' recognition
doesn't read the game's digits, and a reader of Shturmap's own was not built), not the Tasks screen, not the inventory.

**It changes nothing outside its own folders, with one exception the player turns on** (owner, 2026-10-04: "a mode
that auto-deletes screenshots after a couple of seconds grace period"). "Delete position screenshots" in settings,
off unless ticked: a screenshot whose name gave a position is deleted 5 seconds after its name was read
(`ScreenshotCleaner`), for good, not to the Recycle Bin. The 5 seconds leave the game time to finish writing the
file and any other tool the player runs time to read its name. What may go is narrow, and checked when the file is
seen and again before it goes (`ScreenshotCleaner.MayDelete`): only a file the watcher reported as new in this run,
directly in the screenshots folder, named as the game names a screenshot with a position. What was in the folder
before Shturmap looked, a menu screenshot (no position, so taken for its picture) and every other file stay. Nor is
a screenshot that comes back new: a file last written before Shturmap started watching, or before a screenshot of
its name was deleted, is old (`ScreenshotWatcher`; review of 2026-10-09: a Screenshots folder a sync tool or the
Recycle Bin restored had each file it brought back reported as new and, with the setting on, deleted 5 s later). So
restored or copied-in screenshots are neither placed on the map nor deleted. The
cleaner never opens the image: the file is removed by name. One that is still open elsewhere (the game writing it)
can't be removed and is tried again, six times 5 seconds apart, then left with a line in the app log; a file marked
read-only is left. Unticking within the 5 seconds keeps what still waits, and so does closing Shturmap. The folder
is the player's own Documents folder, not the game's install: the game is not touched, asked or told anything, and
"never edits game files" above stands. The setting's note, the README, PRIVACY.md and help say it; the diagnostics
carry the setting's state, for "where did my screenshots go?".

Over the network it downloads public data and art (tarkov.dev, the map artwork), and nothing about the player
leaves the PC unless the player sends a report or allows crash reports (§8, "Reports"). `SafetyTests` fails if the
code names any other address, or if anything but the reporting code uses Sentry. Some addresses come out of the
data instead (maps.json gives each map's artwork and tile paths), so the app's download client itself asks only
`json.tarkov.dev`, `assets.tarkov.dev` and `raw.githubusercontent.com`, over https, and refuses anything else before
a request is sent (`CachedHttp.Hosts`; review of 2026-10-04: a changed maps.json could have pointed every Shturmap at
another host). The links that leave Shturmap come out of the data as well (a quest's wiki page, a map's wiki page):
one is offered only when it is an https address on the Escape from Tarkov wiki (`OutsideLink`); anything else in
that field is no link, since a click would start whatever program Windows has registered for it.

BSG's licence agreement, read literally, covers all companion tools: 4.3.4 (text of 2026-06-15) forbids "outside
software that captures, collects, counts or otherwise 'retrieves' information reproduced or stored by the Game
Launcher Application or the Game", unless BSG permits it "at its own exclusive and absolute discretion"; no tool has
been permitted. The owner accepted that residual risk. Do not widen the boundary: only the owner does, as with the
extract list's corner of a screenshot (above, 2026-10-05).

What players accept and what they don't (web research of 2026-10-03: a dozen screenshot-position tools, BSG's
pages and licence, about a dozen Reddit threads). BSG documents the coordinates in screenshot names itself (support
article 437, on bug reports) and has never removed them; no BSG statement for or against such tools and no verified
ban were found. Players tolerate a map of their own position, above all in PvE; sharing positions with a squad is
called a "radar", and pressing the screenshot key automatically a "bot". Hence two rules that stay (owner,
2026-10-03):

- **Never share a position.** Shturmap shows the player's position to that player only: no squad map, no syncing
  between players, no live sending of positions anywhere. The app log records no positions (only demo runs do, at
  DEBUG), so a report doesn't carry them either.
  One thing comes close, and PRIVACY.md says it: The Lab, Labyrinth and Icebreaker are drawn from tile renders
  (§3), fetched for the part of the map on screen, and in a raid that part is usually where the player is. So
  tarkov.dev's image service can tell roughly where on such a map the player looks. It gets no position, no
  account and no raid, and a tile stays in the cache for a month, so a part seen once isn't asked for again. The
  ways around it were measured and not taken (owner, 2026-10-04): a whole map at every zoom level is about 290 MB
  a layer for The Lab, and a whole map up to zoom 4 only would turn the picture soft when zooming in ("I would
  like to avoid that").
- **Never take a screenshot for the player.** A position comes only from a screenshot the player took with their
  own key press; no timer, no synthesised key, no "auto" mode (on top of "never sends input" above).

And one for every text about Shturmap (README, website, release notes): say what it reads and what it never does;
never use the vocabulary cheat sellers use for their tools ("radar", "minimap", "GPS", "tracker", "undetected",
"safe", "allowed", "ban-proof"); never suggest BSG approves of it; name the risk plainly (4.3.4, no guarantee
against sanctions); and say that some players see a position map as an unfair advantage in PvP.
Where the risk is named, a text may add that 4.3.4, read literally, speaks against companion tools players have
used for years as well (owner, 2026-10-04: "also state that this would also speak against other popular companion
apps that people use for years"; on the website's "Is this allowed? Can I get banned?"). It names tools with what
each reads, from their own pages: TarkovMonitor (The Hideout, the makers of tarkov.dev; on GitHub since July 2022;
it watches the game's log files and reads positions from screenshots) and RatScanner (since March 2020; it takes a
screenshot to identify an item). Neither claims Battlestate's permission. The sentence is context and never the
last word: the risk line follows it, for them and for Shturmap alike. Check the two tools' pages again before
quoting it anywhere new.

## 3. Copyright and data use

- **Shturmap's own code** is MIT-licensed (`LICENSE`; owner, 2026-10-02, to publish it on GitHub). Everything a
  build redistributes is listed with its licence in `THIRD-PARTY-NOTICES.md`; `eng\notices.ps1`, run by the app
  project before every publish, gathers it, `LICENSE` and every package's own licence files into the build's
  `licenses` folder, which is installed with the app (§8, "Distribution"). The help
  panel's LICENCES link opens it. Update it in the same change as any package. Everything Shturmap uses but doesn't ship (data, artwork, ideas) is credited in the README.
- **Game data** (quests, maps, items, extracts, bosses) comes from tarkov.dev's public JSON service
  (`json.tarkov.dev`), run by The Hideout. Its API page says it is free with no rate limit; there are no written
  terms. Requests say who they are (User-Agent `Shturmap/<version>` with the project's address, the build's own
  version without its commit; `CachedHttp.UserAgent`) and are cached with ETags (1 h, 24 h, 7 days by kind).
  Data is downloaded at runtime into the user's cache for personal use and shown with "data tarkov.dev". It is
  never bundled in the repository or a build.
- **No unnecessary load on tarkov.dev** (owner, 2026-10-04: "Check if we properly cache the maps so we don't put
  unneccessary load on tarkov.dev"; checked the same day against the servers). What Shturmap asks, and when:
  - Only at a start and when the mode changes, never on a timer while it runs. A saved copy younger than its age
    is used without a request: quests, maps and traders 1 hour; item names, item sources and `maps.json` 1 day; a
    map's SVG 7 days; a map tile 30 days. Portraits and icons are downloaded once and kept.
  - An older copy is asked for with its ETag. All three hosts (`json.tarkov.dev`, `assets.tarkov.dev`,
    `raw.githubusercontent.com`) answer "not modified" to that, which costs a few hundred bytes. A file that did
    change comes compressed: the maps file about 0.8 MB, the quests 0.4 MB, the item sources 1.9 MB (they change
    every few minutes with the flea market's prices, hence once a day). A start a day after the last one is 13
    requests (18 in another game language) and about 3 MB if every file changed; a second start within the hour
    asks nothing.
  - What the server doesn't have is remembered for a week, the time its own Cache-Control gives its answers
    (`CachedHttp.MissingAge`): a tile or a picture that answered "not found" was asked for again in every session
    before. A refusal (403) is not remembered: it can be a block that passes.
  - While a download fails, each try waits longer than the one before: 2, 4, 8, 16 minutes, then every 30
    (`RetrySchedule`; §8, "Asking again"). With tarkov.dev down, every running Shturmap asked again every 2
    minutes before, 720 times a day; now about 50.
  - The hour for the data stays: it only matters at a start, an unchanged file costs one small answer, and a
    longer age would keep a corrected quest away for longer after a patch. Tiles are fetched by view (§2, "Never
    share a position"); a whole map would be hundreds of megabytes.
  Before anything is added that downloads more or more often (prefetching, a refresh timer, shorter ages, quicker
  retries), count its requests and bytes per player and say them to the owner.
- **Map geometry** comes from tarkov.dev's `maps.json` (MIT, Copyright (c) 2019 Oskar Risberg; the test fixture
  snapshot carries that notice).
- **Map artwork** is the SVG maps by Shebuka and contributors (CC BY-NC-SA 4.0). Their README revokes the licence
  for software "designed to facilitate cheating or gaining an unfair advantage" (radars and ESP overlays, maps
  modified for cheat clients, automation scripts, pixel-bots). Shturmap is none of these: positions come only from
  screenshots the player takes, the game is never touched and nothing is automated (§2). The artwork is downloaded
  at runtime, displayed unmodified except for showing one floor at a time (their README invites that), credited on
  the map with its licence, and never redistributed. Non-commercial only. A published screenshot that shows a map
  shares the artwork: credit it the same way and keep it non-commercial.
- **Maps without SVG artwork** (The Lab, Labyrinth, Icebreaker). tarkov.dev publishes them only as image tiles
  (assets.tarkov.dev; maps.json credits "Tarkov.dev", and TarkovBOT.eu for Icebreaker): top-down renders of the
  game's own level geometry and textures, i.e. Battlestate's art. **They are shown** (owner, 2026-10-03: "Do B and A",
  extending to these renders the terms accepted for trader portraits and item icons below; until then they weren't
  used and the sheet below stood in): fetched from tarkov.dev's image service as a view needs them (the zoom level
  that matches the screen, the base layer and the shown floor's layer, at most four at a time; while one loads the
  nearest coarser tile stands in, stretched), kept in the user's cache (`cache\map-tiles\<map>\<layer>\<z>\<x>_<y>.png`,
  checked again after a month) and decoded in memory (the 192 most recently used, and beyond that
  every tile the view on screen needs: a 4K window takes more than 192, and a tile thrown out and asked for again with
  each frame never finished loading; the review of 2026-10-04), never in the repository, a build
  or the test fixtures, credited on the map ("Map: Tarkov.dev · data tarkov.dev"). Screenshots of Shturmap may show
  them, as with icons. They are placed with tarkov.dev's own transform, through its Leaflet CRS: a map unit is a pixel
  at zoom 0, so the tile (z, x, y) covers [x·T, (x+1)·T] ÷ 2^z map units each way, T being maps.json's tileSize (175
  for The Lab, 256 elsewhere) whatever the image's own size (`TileGrid`; worked out from Leaflet's documented maths,
  no code taken). Checked by eye on 2026-10-03: The Lab's extracts land in their rooms on its levels (the medical
  block's elevator room on Technical, the hangar's floor), its room names on their rooms on both floors, Labyrinth's
  and Icebreaker's loot spots on their decks and rooms. Like SVG artwork the renders recede, the colour treatment
  baked into each tile when it is decoded, so a frame only copies pixels (on the CPU, 16–45 ms a frame for The Lab,
  3–11 ms for Icebreaker and Labyrinth); they are clipped to maps.json's bounds (Labyrinth's render runs on with a
  border line and an opaque background). The Icebreaker render is stretched 1.75× along one axis, so these maps keep
  tarkov.dev's transform (`MapProjection.For`). The svg-maps repository's `Labs.svg` (Shebuka, CC BY-NC-SA 4.0, last
  changed 2023) stays unused: a schematic that isn't to scale, its rooms 13–22 m off when fitted to the level.
- **The sheet** stands in when no tile can be had (offline without saved tiles, or none published), drawn from data
  only: maps.json's bounds (the extent tarkov.dev gives the map, not a traced outline) as a panel with a metric grid
  on whole metres (10 m, every fifth line stronger), tarkov.dev's labels for the shown floor, and everything else as
  on any map (player, trail, objectives, extracts, transits, spawns, floors). No walls or layouts are drawn: none
  exist in the data (owner, 2026-10-02: Shturmap shows only what it has a precise readout for). The sheet says
  "NO ARTWORK FOR THIS MAP · GRID 10 M" in its corner (plus "NO FLOOR DATA" where maps.json has no floors, as for
  Labyrinth: its markers are then all on one plane), the credit line says so, and a notice says it once per map ("No
  map render for The Lab: couldn't download it…"). With tarkov.dev's transform kept, Icebreaker's 10 m squares are
  oblong on its sheet (the lines are still 10 m apart); a map with neither SVG nor tiles would get one scale for both
  axes, so its squares stay square. The credit line names an artist only where their SVG is drawn.
  A sheet that stands in for a render that couldn't be had gives way by itself (the review of 2026-10-04: The Lab
  opened offline stayed a sheet until the app was started again): the tiles the view needs are asked for again
  every 30 s, each tile once a round, whether or not the map is touched meanwhile, and the sheet stays until the
  first tile arrives (`MapTiles`: `Ask`, `RetryAfter`). A tile tarkov.dev doesn't have (404) is not asked for again; one whose
  file isn't an image (a download cut short) counts as one that couldn't be had, and is.
- **Trader portraits and item icons** are Battlestate's art. They are never in the repository, a build or the
  test fixtures. Shturmap fetches each one from tarkov.dev's image service (`assets.tarkov.dev`) the first time it
  is shown, keeps it in the user's cache (`cache\game-art`), and shows a glyph when it can't be had. This is
  display for personal use, the way every Tarkov tool and tarkov.dev itself show them. Screenshots of Shturmap
  on the website and in the README may show them, as the wiki and other community sites do (owner, 2026-10-02,
  accepting that BSG's licence 4.2.2, read literally, doesn't allow it); they are never published on their own.
- **Not used**: other Battlestate artwork (quest images, game UI art, and map art except tarkov.dev's renders of the
  three maps without SVG artwork, above), text from the EFT wiki or guide
  sites (the quest card links to the wiki page instead), and code from other community tools: TarkovMonitor,
  TarkovTracker and MAYAK (GPL-3.0), Tarkov Pilot (no licence), RatScanner (source-available, based on the Elastic
  License). Facts learned from them (log formats, file paths) are reimplemented. The idea comes from TarkovEyes
  (MelGP, MIT); Shturmap is a from-scratch rebuild and takes no code from it.
- **Name and branding**: no "Tarkov" in the app's or the repository's name, no Battlestate logos, typefaces or UI
  art. Logo lettering is drawn from scratch (Windows fonts may be used on screen, not converted to outlines). The
  README says Shturmap is unofficial and that game content belongs to Battlestate Games.
- **Icons** are the Segoe Fluent Icons font that ships with Windows, used in place, never copied into the repo;
  the only custom glyph is the crosshair (drawn from a path in `Shturmap.Map.Glyphs`).
- **Test fixtures** in the repository are the maps.json snapshot only. **Game logs are not in the repository**, not
  even scrubbed (owner, 2026-10-04: "I don't think logs should be in the repo at all if not needed"; until then
  four scrubbed sessions were committed, and the review of that day found ids the scrubber had missed: profile ids
  were masked only after "profileid", so ids on other lines stayed. They were taken out of the whole history the
  same day). The sessions that a few tests replay stay on the PC that made them, in `tests/fixtures/logs`, which git
  ignores; without them those tests are skipped, and the same logic is tested on log lines written by hand, with
  made-up ids. `tools/make-log-fixtures.ps1` makes the local copies under a made-up name, every time in them moved
  to it (2026-01-01, the day all examples use; owner, 2026-10-09), and still scrubs them: an id stays only where it
  is public game data (a quest or trader id in a message's `templateId`, an item template, a trader as the sender);
  every other id is a placeholder (profiles, messages, events, item instances, the stash, raids, the push channel),
  and tokens, session ids, account ids and addresses are masked. `FixtureScrubTests` check the local copies. Quest
  texts are not committed beyond a sample: tests hold up to about seventy one-line objective texts from tarkov.dev,
  the sample the synopsis rules are built against, never a dump and never a quest's full description (owner,
  2026-10-09). The check over all quests reads the user's local tarkov.dev cache.
  `tests/fixtures/ocr` is ignored by git: it holds the owner's own screenshots (Tasks screens from the removed OCR
  feature, and since 2026-10-05 raid screenshots with and without the extract list, which `ExitListReaderTests` read
  where a PC has them and skip elsewhere; the same reader is tested everywhere on a list the tests draw themselves).
  Never commit or publish it; it was removed from the whole history on 2026-10-02, before going public.

## 4. UX principles

1. **No clicks in a raid.** Shturmap follows the game: the logs decide the view, the map, the mode and the side;
   screenshots move the player. Nothing in a raid requires input. Defaults must be right without configuration.
2. **Glanceable from a second monitor.** High contrast on a dark ground, the important line first and largest (the
   glance's distances), sizes by the type roles ("Design system"). No animation beyond what helps the eye follow a
   change, the raid replay at a raid's end (§1's exception), and the tour (§1's second exception, owner, 2026-10-09:
outside raids only, and still in the cues' own motion), and the QUEST COMPLETE cue (§1's third). (Until 2026-10-04 this also said "primary text ≥ 14 px, numbers in a monospaced face"; the app never
   kept to either, and the owner dropped the rule: "the 14px rule is not necessary".)
3. **One window, two states.** *Plan* while in the menus, *Raid* while loading or in a raid. The switch is
   automatic. No tabs, no modes to pick, no modal dialogs. Shturmap has one window, never topmost, so it can't cover
   the game (popped-out quest cards, the only other windows, were removed on 2026-10-09). One thing does
   cover the window, and only because the player opened it: the Report dialog (review of 2026-10-04: the
   principle didn't name it). And the tour, which dims the window around the parts it is about (owner, 2026-10-09,
   the panel's T3: "I want to put an exception to the design for the tour"): at a first start and when the player
   opens it from help or What's New, never in a raid, and Esc ends it from its first frame (*The tour*).
4. **Minimal surface.** Every control earns its place. Prefer an automatic behaviour over a button, a sensible
   default over a setting. Messages are one line and dismiss themselves.
5. **Clear requirements.** What a raid needs (keys, items to bring) is shown before the raid, unprompted, and next
   to the objective that needs it during the raid.
6. **Says why.** Every quest state can say where it came from (the game log and when, or implied by a later
   quest). So can an objective shown as done: "Done · ticked by you, 4 Oct" (below, and "Quest cards", *Ticks*).
7. **Never ask.** No confirmations, no prompts. Quest states come from the game's logs alone (owner, 2026-10-01:
   players don't screenshot their Tasks screen, so Tasks-screen reading and the TarkovEyes import were removed).
   The exceptions, each named where it is described (review of 2026-10-04: the principle didn't name them): the
   one question after a crash (send a report?), the question before an uninstall the player started, and the
   PMC ⇄ SCAV tag of a raid whose side the logs can't tell, which is a switch the player may use, never a prompt.
   Nothing else asks, and nothing asks during a raid's play.
   **One thing the player may tell Shturmap about progress, and is never asked for: that an objective is done**
   (owner, 2026-10-04: "Tick an objective done by hand makes sense"). The logs say when a quest starts, completes or
   fails, never when one of its objectives is done, so a quest that takes several raids kept leading to places
   already dealt with. A tick on the quest's card takes that objective out ("Quest cards", *Ticks*). The quest's own
   state still comes from the logs alone: a quest with every objective ticked is active until the log says
   otherwise. Editing a quest's state by hand stays declined (owner, 2026-10-04); the code for it is gone, and rows a
   database may still hold from it are ignored like the Tasks-scan and import rows (§8, "Quest progress").
8. **Position is occasional.** Players press the screenshot key now and then, not continuously. Everything except
   the "you are here" parts works without a fix. A fix shows its age; the marker stays at full strength and says
   it (see "Map drawing"). No "you may be
   anywhere in here" ring: it was tried and is visual noise. Directions relative to your facing
   ("ahead-left") are shown only for 45 s after a fix; after that they become map directions ("NE", map-up is
   north), which stay true while you move. The map's facing cone shows for the same 45 s, from the same constant
   (`Facing.Fresh`; the review of 2026-10-04: the cone stayed for 60 s, so for a quarter of a minute it still pointed
   "ahead" when the cards no longer said so. The shorter of the two, since the facing is only true for a moment).
   Distances say how old they are. **A new fix moves the view only while
   Follow my position is on** (owner, 2026-10-03: "It should be a toggle in the map view and the camera should
   smooth scroll to the updated player position"). It replaces the rule of 2026-10-01, "a new fix never moves the
   view", made when the study log showed the player zooming back out within seconds of every automatic framing; so
   following is the player's choice, off by default, and it keeps their zoom (see "Map", "Follow my position").
   Every new position pings, three bold sand rings leaving the marker over 2.6 s (two still rings with animation
   effects off). Following, the view glides to it. Not following, the map stays where the player put it; out of
   view, a badge at the edge
   of the map points to it with an arrow and pings, says "YOUR NEW POSITION · PRESS F", and a one-line notice says
   so; the badge stays (quietly) while the position is out of view, and clicking it, F, or the map button shows
   the position at the current zoom. An old screenshot found at start doesn't ping. When the raid ends, the
   player marker and trail go: out of a raid there is no "you". The raid's positions, with their minutes, stay in
   memory for its replay until the next raid loads ("Map drawing", *The raid replay*). So while the game's logs are followed, a position
   the log shows no raid for isn't plotted, and a notice says so ("Got a position, but the game's log shows no
   raid, so it isn't shown."; nothing is said for a screenshot taken just before the raid's end line reached the
   log). Only with no game logs at all, when nothing can say where the game is, does the shown map take a position
   that lies on it (review of 2026-10-04: with the logs followed, a position in the menus used to be plotted on the
   shown map whenever it fell inside its bounds). A position's age is said beside the marker: from
   a minute, the ring turns dashed and a tag gives the age ("1 MIN OLD"; with "OLD" from the first minute since
   2026-10-04, bare minutes on a map read as a time to get somewhere); from 2 minutes the tag ("7 MIN
   OLD") is larger and framed in sand (the study log: about one position per 8 raid minutes, often several minutes
   old when the app was looked at). Until 2026-10-03 the 2-minute case was also said over the map in big gold type
   ("POSITION 7 MIN OLD · PRESS PRTSC OR HOME FOR A NEW ONE", and "NO POSITION YET" a minute into a raid without
   one); the owner had it removed: "Put it next to the marker". No position yet is said by the status bar and the
   raid card.
9. **Say it before it matters.** When a raid starts loading, Shturmap shows what to bring for that map, while there
   is still time to back out of matching. **The kit reminder** (owner, 2026-10-03: the one-line "Loading … · bring:
   …" notice came too late to read and as a list of words; "it could still be a good reminder what to bring … with
   icon previews"; "If the player sees they forgot something they can still cancel loading into the map"):
   - **When:** the scene line names the map 1–2 s after matching starts, before matching completes (2–25 s) and
     before a server raid's match setup (15–71 s); in the study logs (a few dozen loads over seven weeks)
     matching was cancelled 9–27 s after it started, so it is the earliest line naming the map and still in time.
     A group's pick comes earlier still (below). A cancelled load goes back to the menus, which ends it.
   - **What** (`Planning.Kit`): every active quest's BRING row for the map; what it takes to get in or out first
     (the entry item, the exits quests name: a flare, climbing gear, money), then what the picks need, then, with
     picks on this map, the rest under ALSO USEFUL. Without picks there it is all one list. Shturmap knows no
     inventory, so it is a list to check, never a claim that something is missing.
   - **The cue** (RAID LOADING, GROUP PICKED) pictures it under the map's name, under a small "CHECK YOUR KIT":
     **every item, in rows of eight, up to three rows** (then "+3"), in 52 px cells, with "×3" on a picture where
     several are needed; never scaled, like the cue's text. It is a reminder to take in at a glance, not a list to
     read (owner, 2026-10-04: "when many items are required, those get quickly hidden behind a +x mark while we
     still have plenty of screen space ... this should not be something that requires a lot of 'reading', it
     should be a gentle reminder of 'did I pack everything?' that might trigger a cancel when loading to stock up
     on missing items"; until then six pictures of 34 px in one row). The pictures come in one after another once
     the title stands, 50 ms apart, so the eye is led along them. **With picks, what they need comes first, each
     cell framed in its pick's colour, then a hairline and the rest in the kit's order** (owner, the same day: "it
     should also show color coded the icons first of the quests we highlighted"; read as the items of the picked
     quests, not the quests' own glyphs). **Without picks it is one plain grid** in the kit's order, what gets in
     and out of the map first ("some people are not working with highlighting missions at all, so it has to
     account for this"). A cue that pictures a kit stays 7.5 s where the others stay 5 ("The animation can be a
     bit longer"), and a quarter of a second more for each picture past the first row, up to 11 s. A transit's
     cue has none (the gear is what the raid had).
   - **The raid card** starts with CHECK YOUR KIT, in BRING's rows, while the raid loads; BRING itself steps aside
     until the raid starts, then is back in its place. A Scav loading (known early when a server raid's setup names
     the Scav profile) gets no kit, and the card's note says why: quest objectives don't count for a Scav.
   - The study log notes it (`kit.reminder`: when, map, items, items for picks).

   In a group it comes earlier: when the leader picks a raid (the notification log says so 20–70 s before loading
   starts), Shturmap shows that map with the GROUP PICKED cue and its kit (owner, 2026-10-02). The pick's time of day
   ("CURR" or "PAST", the two raid times 12 h apart) isn't shown: the clock formula checks out against the
   screenshots' raid clocks, but which of the two times each name means couldn't be checked without a group raid
   with a screenshot; the study log now records both to settle it. What a quest needs is also beside its name wherever
   it is listed for a map (Plan cards, the raid card): up to three tiny inventory cells with the items' icons,
   "+N" for more (owner, 2026-10-01: BRING alone didn't show which item is for which quest), and "×3" right after a
   cell where the quest needs several (owner, 2026-10-05: "If multiple of the same, it should say so with a number";
   beside, since a figure inside an 18 px cell would hide the item). Each quest's cell says its own number, a map's
   row of cells the whole row's (`Requirement.CountFor`); a quest that needs
   nothing shows no cell (owner, 2026-10-04: the empty, dashed cell of 2026-10-01 stood on most rows and said no
   more than its absence; the Plan cards' rank numbers "1", "2" went the same day, the order says it). ANY MAP rows show no cells:
   bringing doesn't apply there.
10. **Two clicks, two meanings.** A click on a quest keeps its card open. Its **pen** (on the row while the quest is
    pointed at or picked, and always on its card) picks it for the coming raid: lit on the
    map and first in the rail, several at once ("Picks"; owner, 2026-10-01: one click doing both was misleading; the
    study log had the player toggling quests on and off and losing held cards on the way to the map).
11. **Show when the view changes by itself.** When Shturmap changes its view without being asked (a raid
    loading, a transit, a Scav raid starting, the raid over, loading cancelled, the group picking a raid), a cue
    holds the middle of the map for 5 s (owner, 2026-10-01; first 2.8 s, then longer, sharper and with more pop). Its
    entrance plays at 1.8 times its first pace, about 1.6 s (owner, 2026-10-02: the elements should appear more
    slowly; a briefly tried 8 s RAID LOADING cue was too long): a dark band springs open
    behind the text with a short gold flash, gold rules shoot out from the centre with a slight overshoot, and the
    map's name (or RAID OVER) slides up while it decodes letter by letter like a terminal, undecoded letters
    flickering in gold and settling in ink, with one line under it (what the raid can do, or the next raid
    suggested); then it fades. Nothing with text in it is ever scaled, so the text stays sharp. It takes no clicks.
    RAID OVER of a raid with a replay gives way after its entrance to the replay's band at the map's foot ("Map
    drawing", *The raid replay*).
    **QUEST COMPLETE** (owner, 2026-10-07: "For completed quests, also make a nice animation"): when the log reports a
    quest completed (handed in at its trader; in one week's study log all twenty-odd completions came in the menus,
    most two to four within a minute and a half), the cue says "QUEST COMPLETE · PRAPOR", the quest's name decoding as
    every title does, and what it unlocks ("Unlocks Setup and Shooter Born in Heaven", the quest card's UNLOCKS), under
    a stamp: the done objective's check on the quests' gold, popping in as a gold ring leaves it (no text in it, so
    scaling it blurs nothing). One that comes while the cue is up joins it, "QUESTS COMPLETE", "3 QUESTS", their names
    and what they unlock together, its title decoding and the stamp popping again (`Rules.CompletionWords`). It stays 5 s
    after the last quest joined, 12 s at most, then fades. It replaces the one-line notice "… completed" (a start or a
    failure keeps its notice). One that comes while a replay plays waits for it to end; never for the log read back
    at start. On the map the quest's places ring out ("Map drawing").
    Picks the player makes (a Plan card, the map
    list) show no cue, and neither does Follow my position's glide: the player turned it on, and the toggle and the
    ping say it. With animation effects off it shows and goes without motion. Never during the log replay
    at start.
12. **Point, don't navigate.** Pointing at a quest, an item or an extract anywhere lights up every other place it
    appears (rail rows, cards, map markers) and, for a quest, shows its card. Details come to the pointer; there
    are no detail pages to open. Finished quests appear nowhere. The pointer says as much as it is on: on one
    objective of a quest (its line in the raid card, its row on the quest's card, one of its places on the map) the
    quest stays lit and that objective is marked within it; on a need cell beside a quest's name, or on the gold
    line that names one key, it is that item (the review of 2026-10-04; "Visual language", linked highlight).

### Design system

One set of colours, type, shapes, motion, icons and words for the app, the map, the README, the website and the
release notes (owner, 2026-10-02: "make sure the colours, fonts and general design are consistent in the app, the
readme and the future website"; written down 2026-10-03). The sections below ("Visual language", "Logo", "Map
drawing", §2's wording rules) apply it; when they say a colour or a style, it is one of these.

**Colours.** The table is the source: `Shturmap.Map.Palette` holds it in code, and `DesignTokenTests` fail when
App.xaml, a XAML colour, the map, `brand\build.cs`, the website's CSS or this table disagrees, or a colour appears that
isn't here. A colour may appear at a lower alpha (gold at 18 % behind linked rows, ground at 78–94 % behind overlays);
a new hue may not. Each kind colour has one meaning (owner, 2026-10-03: one symbol, one meaning, for colour too).

| token | hex | role | where (App.xaml · map · website) |
| --- | --- | --- | --- |
| `Ground` | `#0B0C0B` | the page and the map behind everything; dark collars and halos | GroundColor · Background · `--ground` |
| `Rail` | `#101110` | the rail and status bar | RailColor · — · `--rail` |
| `Panel` | `#151614` | cards, flyouts, tooltips, the icon's plate | PanelColor · — · `--panel`, `--grid` |
| `Raised` | `#1E1F1B` | the selected or expanded thing, one step lighter | RaisedColor · — · `--raised` |
| `Line` | `#2A2B27` | hairlines | LineColor · sheet grid · `--line` |
| `LineStrong` | `#45463F` | the stronger hairline of what is selected; frames | LineStrongColor · sheet edge · `--line-strong` |
| `Cell` | `#1A1B18` | the inventory cell behind item icons | CellColor · — · — |
| `Ink` | `#D9D5C4` | text; on the map every label, and the symbols that are neither a quest's nor the player's (spawn rings, padlocks, switches, an item's loose spots) | InkColor · labels, Ink · `--ink` |
| `Muted` | `#8A8778` | secondary text; what happens after the raid or at a trader; done objectives | MutedColor · Muted · `--muted` |
| `Amber` | `#C9AD62` | the one accent: quests, objectives, distances, "on" | AmberColor, SystemAccentColor · Amber · `--amber` |
| `AmberHover` | `#D6BE7E` | the accent under the pointer | SystemAccentColorLight1 · — · `--amber-hover` |
| `AmberHi` | `#E2CF9C` | focus rings, link hover on the web | SystemAccentColorLight2 · — · `--amber-hi` |
| `AmberDeep` | `#A88F4E` | the accent pressed | SystemAccentColorDark1 · — · — |
| `Green` | `#8DA65E` | PMC extracts, success, healthy inputs | GreenColor · Green · — |
| `Teal` | `#6F9A94` | Scav extracts | TealColor · Teal · `--teal` |
| `Khaki` | `#B7B77A` | extracts for both sides (the split triangle) | KhakiColor · Lime · — |
| `Violet` | `#9C8CC4` | transits | VioletColor · Violet · — |
| `Red` | `#B8604A` | bosses, danger | RedColor · Red · — |
| `Sand` | `#E9E2C8` | the player: the marker, the trail, the ping, the edge badge, and nothing else | SandColor · Player · `--sand` |
| `Kept` | `#3FD2E0` | the dev build's icon plate (until 2026-10-04 also every picked quest) | KeptColor · — · — |
| `Pick1` | `#8DD3C7` | the first pick: its markers, name, badges, row tint and pen | map · code |
| `Pick2` | `#FB8072` | the second pick | map · code |
| `Pick3` | `#80B1D3` | the third pick | map · code |
| `Pick4` | `#B3DE69` | the fourth pick | map · code |
| `Pick5` | `#FFFFB3` | the fifth pick | map · code |
| `Pick6` | `#FCCDE5` | the sixth pick | map · code |
| `Pick7` | `#BEBADA` | the seventh pick | map · code |
| `Pick8` | `#FDB462` | the eighth pick; a ninth takes the first colour again | map · code |
| `LightGround` | `#F1F0EC` | the logo's light background | brand |
| `LightInk` | `#1E1F1B` | the logo's ink on light | brand, README light mode |
| `LightAmber` | `#8C7436` | the logo's amber on light | brand, README light mode |
| `LogoMarks` | `#4A4A41` | the icon plate's corner marks | brand |
| `LogoGrid` | `#171815` | the social preview's grid | brand |
| `DeveloperPlateEdge` | `#1E6F78` | the dev icon plate's edge | brand |
| `DeveloperPlateMarks` | `#2A97A3` | the dev icon plate's corner marks | brand |
| `WebInkSoft` | `#BDB9A8` | long reading on the website (leads, answers) | website `--ink-soft` |
| `WebDim` | `#6E6C60` | parts of a file name Shturmap doesn't use (website) | website `--dim` |
| `WebTealText` | `#8FB8B1` | the facing in a file name, as text (website) | website `--teal-text` |
| `SheetPanel` | `#121311` | the map sheet's panel (maps without artwork) | map |
| `SheetMinor` | `#1C1D1A` | the map sheet's 10 m lines | map |

**Type.** Bahnschrift in the app and on the map (ships with Windows, DIN-like, close to the game's lettering). It
may be shown on screen but not served as a web font (Microsoft's font FAQ), so the website uses DIN-like faces with
the SIL Open Font Licence, self-hosted: Barlow Semi Condensed for what the app sets semi-condensed, Barlow for body
text, IBM Plex Mono for numbers and file names. The README uses GitHub's own type. Roles, app style and size (px) ·
website class:

| role | app | website |
| --- | --- | --- |
| section label: uppercase, spaced, muted | `EyebrowText` 11, semi-condensed semibold, 0.14 em | `.eb` 12, Barlow SC 600, 0.18 em |
| status word | `StatusText` / `StatusBarText` 12, semi-condensed semibold, 0.08 em | the route readout 13, 0.12 em |
| title (map, quest) | `TitleText` 17, semi-condensed semibold | headings, Barlow SC 600: h1 34–56, `.h` 40 (32 on phones), questions 21 |
| body | the implicit TextBlock style, 14 | body 17, line height 1.6 |
| note, secondary | `NoteText` 12.5, muted | `.note` 14, captions 13, muted |
| figure (distances) | `FigureText` 15, semi-condensed semibold, amber; 22 in the glance | IBM Plex Mono: section numbers, legends, code |
| the big cue | 13 / 48 / 15 | — |
| the tour's band | 13 / 30 / 15: eyebrow, title, the chapter's lines | — |

New text uses a role, not a new size; the few in-between sizes in XAML (10, 10.5, 11.5) are badges and hints that
fit a fixed box.

**Shape and spacing.** Square corners everywhere (`ControlCornerRadius` and `OverlayCornerRadius` 0, no
`border-radius` on the web); flat panels; 1 px hairlines in `Line`, the selected one in `LineStrong`; map symbols wear
a dark collar (see "Visual language"). Spacing in the app steps 2–3 px inside a row, 6–10 px between lines, 12–18 px
between groups, 20 px above a section label; the rail is 384 px, the status bar 40 px. The website lays out on a 96 px
grid with a 72 px reference column, rows 80 px apart (52 px on phones), gaps from 8 to 56 px.

**Motion.** Slow enough to read, never for show: the big cue takes 5 s (7.5 s when it pictures a kit, about 17 s
when RAID OVER replays the raid, §1's exception) with its entrance at 1.8 times the original
pace; the linked highlight eases over 0.18 s, waits 0.25 s before following a lost focus, and pulses every 1.4 s; a new
position pings, and with Follow my position on the view glides to it over 2.4 s, eased in and out; the raid card's time
doesn't move: at a new minute its figure and rule just change (owner, 2026-10-09: its figure decoded and a dark notch
ran along its rule at each minute until then). With Windows' animation
effects off the app shows and hides without motion, and following jumps to the position. The tour (§1's second
exception) moves more, in the same vocabulary: the window dims in 0.35 s, its cut-outs glide from part to part over
0.65 s eased in and out while the map sheet's corner marks close in on them with a small overshoot, the band's gold
rule shoots out and its title decodes over 0.7 s at each chapter, a stage's lines rise in one after another, and its
staged moments are the app's own (a drawn pointer as the website clip's, the screenshot key pressed, the ping, the
pulse, the cue); with the effects off each chapter shows its end at once. The website follows the
reader's "reduce motion": what moves only with the reader's own scrolling stays (the logo, the route marker, without
easing; owner, 2026-10-02), the rest stops.

**Icons.** Segoe Fluent Icons in the app, drawn glyphs on the map (`Glyphs`), one symbol with one meaning everywhere
("Visual language", the list of checked symbols); quest types by glyph, never by colour. Text links that leave
Shturmap end in "↗". The website draws its few symbols itself, in the same colours.

**Logo.** As in "Logo": the full mark from 48 px, the plain Ш from 16 to 40 px; the lockup at least 30 px tall (the
website's phone header; 40 px in the README and the website header); keep a free space of at least half the plate's
height around it; dark and light variants as in the table.

**Words.** Sober and short, units on numbers, no marketing and no jargon (§8, "Rules"); what Shturmap reads and never
does, the risk named plainly, none of the cheat sellers' vocabulary and no hint of Battlestate's approval (§2). The
same in the app, the README, the website and the release notes.
**Explanations in the app are terse** (owner, 2026-10-05: "The explanation text in the app and in the settings are
way too long. Make them more concise, not exactly caveman style but in that direction"). Help, settings' notes,
tooltips and the legend say what a thing is or does in as few words as stay clear: fragments are fine ("Crashes stay
on this PC"), one point per sentence, no "Shturmap does X so that Y" when X is enough, nothing the control or the
swatch beside it already shows. What a text must say (§2: what is read, kept or sent; what a setting deletes; the
legend's shape and colour, "Legend") stays, in fewer words; the reasons belong here, not on screen.

**Where each surface takes it from.** The app: `App.xaml` (colour resources, brushes and the text styles) and
`Shturmap.Map.Palette` for the map. The website: `assets/site.css`'s `:root` (colours under the tokens' names,
`--f-cond`, `--f-body`, `--f-mono`). The README and the website: `brand/` (the logo files, dark and light). Release
notes: plain Markdown, the same words. A change to the design system changes this section, `Palette`, App.xaml and the
website's CSS together, and `DesignTokenTests` check the colours.

### Visual language

The game's own UI, pared down: what a Tarkov player already reads at a glance, with nothing added for show.
No stock Windows look (no Mica, no rounded Fluent controls, no pills).

- **Shape**: square corners everywhere (`ControlCornerRadius` 0), flat near-black panels, 1 px hairlines; the
  selected/expanded thing is one step lighter with a stronger hairline, like a selected slot in the game.
- **Type**: Bahnschrift (ships with Windows, DIN-like, close to the game's lettering). Labels and status words are
  semi-condensed, semibold, uppercase and spaced (`EyebrowText`, `StatusText`); titles semi-condensed semibold
  (`TitleText`); body text regular; numbers semibold gold (`FigureText`). Styles live in `App.xaml`; cards and
  flyouts set the font themselves (popups don't get the app's implicit text style), and so does the rail, on its
  scroll view: the implicit style doesn't reach text inside list templates either, so the raid card's quest names
  and objective lines, the extract rows and BRING in a raid stood in Segoe UI and white until 2026-10-04, beside
  Plan's card in Bahnschrift.
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

Colours and their one meaning each: the token table in "Design system" above (gold for quests, green / teal /
khaki for extracts by side, violet for transits, red for bosses, sand for the player, `Pick1` to `Pick8` for the
picked quests, one each ("Picks", *A colour per pick*), muted for what happens after the raid). Cyan (`Kept`) is
the dev build's icon only: it was every pick's colour until 2026-10-04.

**One symbol, one meaning** (owner, 2026-10-03: the quest card's pin button and the Place quest type were the same
pushpin, so the type beside the trader portrait read as a useless second pin). A symbol stands for one thing
everywhere: rows, cards, map markers, the legend, buttons. The pushpin `E840` is only the Place type (the card's
button that kept it as a window became a box with an arrow out, `E8A7`, that day; it went with popped-out cards on
2026-10-09). Text links that open something outside
Shturmap (LOG FOLDER, PRIVACY, LICENCES, WIKI MAP, a quest card's WIKI PAGE) end in "↗". A check of all symbols the
same day, by rendering them side by side:
- **Two meanings, resolved:** `E719` (a shopping bag) was both the Find-in-raid quest type and the flea market as an
  item source on item cards. The quest type keeps the bag (it is on rows, map markers and the legend); the flea market
  became a price tag, `E8EC` (owner, 2026-10-03, taking the recommendation).
- **Close but distinct:** `E707` (a pin standing on a surface) marks loose spawns as an item source; it is not the
  pushpin. `E81D` (a disc in a ring, "Show my position") matches the player's own map symbol, the same meaning.
  Follow my position (2026-10-03) is another action, so it has its own symbol, `E759` (a dot in a ring between four
  arrows), used nowhere else.
- **One meaning, two symbols:** the Trader quest type is `E716` (two people); a trader portrait that hasn't loaded
  falls back to `E77B` (one person).

Quest **types are shown by glyph, never by colour**; colour stays free for state (open, done, selected).
The type glyph is always the first thing on a quest's row and the only thing inside its map marker. **Trader
portraits** are secondary: small, at the right end of Plan rows, before the quest line of Raid rows and in the card
header; never in place of the glyph, never on the map. **Linked highlight**: gold at 18 % behind the rows that show the very thing pointed at (the same quest, item,
objective or way out, here and elsewhere), and at 9 % behind what only belongs to it (the items a pointed-at quest
needs, the quests a pointed-at item is for); an item that merely shares a quest with the item pointed at is not lit
(owner, 2026-10-04, from the review: one tint stood for all three, and pointing at an item lit every other item
of its quests; `LinkStrength`); map markers
not in focus step back by kind (see "Map drawing", "Stepping back"), easing in and out over 0.18 s, and the focused
ones pulse: rings leave the marker and fade, one every 0.7 s (owner, 2026-10-04: "I really like it, but the effect
is too subtle": two rings half the 1.4 s period apart now, each 4.5 px wide at the start and travelling 38 px on a
dark band, where there was one of 2.5 px that travelled 26 px and faded with the square of the way; and the
pointed-at quest's places out of view get the same pulse at their chevron on the edge, drawn half as large
again, since the small chevron went unseen: "if a quest marker is outside of the current viewport, it should
probably indicate that") (motion is noticed before anything else; off, with the
easing, when Windows' animation effects are off, and only while something is in focus). A pulse or a ping draws the
map again about 60 times a second and moves nothing, so a frame's layout (which marker, badge and label goes where)
is kept from frame to frame while what it is made from stands: the view, the display's scale, the scene's data and
the minute of the player's age tag (`MapRenderer.LayoutOf`, `MapScene.LayoutVersion`; the review of 2026-10-04: every
frame placed every marker and label anew). The marker under the pointer is looked up in the same layout. Losing the focus waits
0.25 s before the map follows, so moving from one row to the next switches the highlight straight across instead
of making every marker blink. The focus is the pointer's, and it lets go when the pointer can't be said to be there
any more: a row that is rebuilt under it (a pen click, a snapshot that changes what it says; review of 2026-10-09:
until then every snapshot, `RowLists`) lets go as it leaves, and when Shturmap's window isn't the active one any
more the focus is dropped, so the map doesn't draw its pulse behind the
game for a whole raid (2026-10-04); pointing at something lights it again, in an active window or not.
**Inside one another** (the review of 2026-10-04): linked things may lie inside a linked row, and the innermost one
around the pointer is the one pointed at; leaving it gives the pointer back to the row around it, which it never
left (`PointerNest`; before, the highlight knew one element, and leaving an inner one let go of everything). So
the highlight goes **down to the objective**: an objective's line under its quest in the raid card, its row on the
quest's card and the glance's NEXT name their objective. Pointing at one keeps the whole quest lit as before and
marks the objective: its own line takes the tint (in the raid card once more, over its quest's), and on the map
only that objective's places pulse, take the pointed-at name, and get the chevrons; the quest's other
places stay at full strength and hold still (no symbol grows when pointed at since 2026-10-05), and the doors of its keys keep their pointed-at
look without the pulse (`MapScene.FocusObjective`). It works the other way too: pointing at one of an objective's
places on the map lights its line in the raid card and its row on the quest's card. An objective with no place on
the shown map leaves its quest lit and nothing pulsing. Three smaller things link as well: a **need cell** beside
a quest's name is its item (its card, its BRING row, the item's other cells, its locks and loose spots; the tint
lies over the cell), the **gold "Key: …" line** under a raid objective is that key when it names exactly one (with
alternatives or two doors it couldn't say which, and stays part of its objective's line; `Planning.NeedKey`), and
a **quest's glyph on a folded Plan card** is that quest. A card opened from one of these small things opens beside
the row or the Plan card it lies in, at its own height, not over its neighbours. For the cards, an objective's
line is still its quest's block: it opens no card of its own, and a click on it holds the quest's.
**A card is its subject while it is read** (the review of 2026-10-04, E3): the whole body of a quest's card (its
header, the gaps between rows, the headings, the frame's padding) keeps the quest in focus, and the body of an
item's card keeps the item and the quests it is for, so moving from a row onto the card it opened no longer lets go:
the quest's places stay lit, and a key's locks and an item's loose spots stay on the map, also while the pointer is
on the card's "Loose on …" rows. The body isn't tinted for it and opens no card (`Linked.Also`, `Linked.AlsoItem`,
`LinkFocus`); rows on the card are still their own thing, the innermost around the pointer.
**A row that stands for several items is each of them** (E4): "A or B" (either key opens the door), gear worn
together, a weapon class. It pictures the first, and until 2026-10-04 it was linked to the first alone. Now it
lights up when any of them is pointed at, and pointing at it lights every row and cell of each of them
(`Linked.Items`, `Focus.Alternatives`, `LinkStrength`); its card is still the pictured item's.
**A door is its key, and it is for quests** (E5): a quest lit its doors, a door only its key. Pointing at a padlock
on the map puts the quests that need its key on this map in the focus beside the key (`LinkDoor`, from the map's
`QuestKeys` read the other way round), so their rows take the weaker tint, as the quests of any pointed-at item do.
The rail's BRING rows, the need cells and the map follow the same rules: a row of several items lights each one's
locks and loose spots, and an extract row's item picture is that item. **A boss's name is its spawn zones**: in the
raid line and under a map's name in Plan, each fact is a part of its own (`LinePart`, laid out by `WrapRow` so the
line still reads and wraps as one), and a boss's part stands for the groups of its markers on the map shown
(`GameData.BossMobsOn` gives the id those groups are made of). Pointing at "Kaban 75%" lights Kaban's zones, and
pointing at one of them tints the name with the weaker tint. While another map is shown the names are plain text.
Markers on another floor than the one shown are drawn at full strength, highlighted
like any other, with a small dark disc at their upper right holding an up or down chevron (owner, 2026-10-01: half
strength read as "unimportant" and hid highlighted markers). Two or more floors away the disc becomes a small plate
with the chevron and the number of floors, counted in the map's floor list ("▲ 4": Streets has six levels, so up or
down alone didn't say how far; cartography review, 2026-10-02). A floor without artwork of its own isn't in that list
(Customs' 4th floor in the oil rig, drawn in the base layer); a place on it counts by its building's floors in height
order (`FloorResolver.Ladder`), so it reads "▲ 2" from the 2nd floor (owner, 2026-10-05: the first WI-FI camera of
Provide Viewership, 14 m up, said "7 M UP" in the raid card and showed a down arrow: counted as the ground it is drawn
in). A marker's badges wear the marker's own colour: the
floor badge like the count and "OPT" (the review of 2026-10-04: it was the player's sand, which says "you"). Badge places are fixed: the floor at the upper
right, a cluster's count at the lower right, an optional objective's "OPT" at the upper left; beside small symbols
(Scav and sniper zones, bosses) the floor badge moves out so the symbol stays visible. Spawn zone markers carry it
too, by the height of their centroid.
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
- **Colours.** The design system's tokens ("Design system"). On light backgrounds ink is `LightInk` `#1E1F1B` and amber `LightAmber` `#8C7436`; the icon keeps its
  dark plate on both.
- The lettering is drawn from scratch as paths, never from font outlines (§3).

### Screen anatomy

- **The window** (owner, 2026-10-04: remember the window's monitor and size). It opens where the player left it: on the
  same monitor, at its bounds, maximised or not (`WindowPlace`; `window.place` in shturmap.db, saved 0.6 s after a move
  or resize has settled and when the window closes; a minimised window keeps what was saved before). At a first start,
  and whenever the saved monitor is no longer connected in the same place, the first start's rule applies: maximised on
  the first monitor that isn't the primary one (the game's), or on the only monitor at 1600×1000. That size, also the
  one a maximised window goes back to, is at the monitor's scale, 40 in from the corner of its work area and no larger
  than it (`WindowPlace.First`; owner, 2026-10-09: on one monitor the size was taken as pixels, 800×500 at 200 % and
  past the edge of a 1366×768 screen). Until 2026-10-04 that rule ran at every start, so a third monitor or the player's
  own size was lost each time. A maximised window sent to another monitor comes back maximised there. The window is
  never smaller than 900×560 (at the monitor's scale): the status bar's lights and three buttons stay in view, and in a
  narrow window the lights' words go first, then its last word, the last fix, trims (see "Status bar"). The minimum is
  for what must stay readable, the status bar and the rail's glance (the raid card's head, NEXT, EXIT); at that size the
  map is for a laptop beside the game, not for reading from a metre away on a second monitor (owner, 2026-10-09).
  Snapshot and demo runs and a given size (`--window`) place the window themselves and remember nothing. The app log
  says where it opened ("Window where it was last: 1300×800 at …").
  A saved place is checked at its monitor's scale, the smallest window and the inset from the corner too (review of
  2026-10-09: they were taken at 100 %, so at 150 % or 200 % a window under the smallest came back as it was).
- **The map on screen** (owner, 2026-10-06: "remember the last played and selected map and return to that one a) when
  a raid ends and b) remember the last open map when the app closes and re-open it"). The map last on screen is kept
  (`lastMap` in shturmap.db, `GameSession.LastMapSetting`): one picked in the MAP list or Plan's list, a raid's own map
  as it loads, a group's pick. The app opens on it; only at a first start, with none kept, on the best suggestion for
  the next raid (until 2026-10-06 the rule at every start, so the map the player had chosen was lost with each start).
  When a raid ends its own map is on screen, also when another was being looked at during it, and is the map kept. The
  log read back at start leaves the kept map alone: a raid in it, over before the app started, is older than the map
  it was closed on; a raid still running at start shows its own map.
- **Status bar** (top): mode (PvE/PvP/Seasonal), raid state, last fix, as uppercase words; on the right the inputs
  (logs, screenshots, data), each with a small square light that turns gold when something needs attention, and
  three buttons of one size (28 px, 6 px apart): **feedback** (a speech bubble, `E939`: the Report dialog, PROBLEM
  preselected; muted and disabled where the build can't report, its tooltip saying so), **?** (help, F1) and the
  **gear** (settings, `E713`, Ctrl+,). Owner, 2026-10-03: "It's not clear that the settings are with the
  questionmark on the top right. It should probably be a questionmark for the help and then a settings button next
  to it", and "there should be a separate feedback/bugreport button".
  The mode is a plain label from the game's log, with no chooser (owner, 2026-10-03, replacing a PvE/PvP/Seasonal
  dropdown). Evidence: every session in the study's application logs (a few dozen over seven weeks) has `Session
  mode: Pve | Regular | PvpSeason` 8–11 s after the game starts, a switch within a session writes a new line (seen
  twice), and the dropdown was never used (no `mode.pick` in the study log). Its tooltip says where the mode
  comes from, and only what was read: "From the game's log, 21:52" (the line's time, with the day when it isn't
  today); before any line, "The mode you last played; follows the game once it starts" (the saved `mode` setting);
  with no game on this PC, "No game on this PC: the mode you last played". A mode name Shturmap doesn't know (a
  future one) keeps the last known mode, says "The game says 'X', which Shturmap doesn't know yet…" and is logged
  as a WARN. With no game on this PC the label becomes a chooser (PvE / PvP / Seasonal, saved like the logged mode):
  there is no log to say it, and the maps and data to browse differ by mode (owner, 2026-10-03, the no-game
  fallback; see *No game* below). It goes back to the label as soon as the game is found.
  The raid state says only what the log shows (owner, 2026-10-03: "in the menus" while the game wasn't even
  running was misleading): "LOADING CUSTOMS", "IN RAID · CUSTOMS · PMC · 12 MIN", and otherwise "NOT IN A RAID",
  never "in the menus" (`RaidStatus`). The application log has no line that marks the game quitting: the sequence
  "Disposing BEClient … BEClient exit successfully … Dll released" that ends some sessions also comes right after
  startup and between raids, and about half of the sessions in the study logs (a few dozen) simply stop after an
  ordinary line. So the log can't tell the menus from a closed game, and "GAME CLOSED" can't be said truthfully; the
  tooltip says so ("The game's log shows no raid (it can't tell the menus from a closed game)"). No process checks
  (§2).
  **A raid whose end never reached the log** (review of 2026-10-04). For the same reason a raid the game was closed
  or crashed in keeps its start line and never gets its end line: read back at the next start it showed "IN RAID ·
  CUSTOMS · PMC · 1,310 MIN", and the next game start's login line then ended it as a raid of 22 hours. Only two
  things the log does show say such a raid can't still be running: a newer log session than the raid's (the game
  has started again), and the time since it began. So an open raid or load is closed, as "end not in the log", when
  the game has started again, or when it began longer ago than its map's raid length plus 30 minutes (two hours
  where the length isn't known; `UnfinishedRaid`). The margin is generous on purpose, since dropping a live raid
  would be the worse mistake: tarkov.dev's length may be behind a patch, a Scav's or a transit's timer isn't the
  map's, and the end line only comes after the screens that follow a raid. It is looked at after every batch of log
  lines, when the data arrives, and every 30 s while the app runs (a log that has fallen silent sends nothing). The
  state is then "NOT IN A RAID"; the last-raid line says "LAST RAID · CUSTOMS · END NOT IN THE LOG", with no
  length, since nothing says how long it ran; RAID OVER comes without minutes, and not at all while the logs are
  read back at start. A raid within its bound is still shown as a raid: the log shows one, and can't say more.
  Everything in the bar shares one middle line (a player's report, 2026-10-03): the status words use the tight
  line box of their capitals (`StatusBarText`, `TextLineBounds="Tight"`), so centring a word centres its capitals,
  and the lights, the three buttons' frames and their symbols are centred on the same line; vertical padding keeps the
  words with tooltips easy to point at.
  **Words where they apply, and what gives way** (review of 2026-10-04, C4; `StatusBarFit`). While there is no
  position the bar says "NO POSITION YET · PRESS PRTSC" only in a raid, where the key gives one; outside a raid and
  while one loads it says nothing there ("… IN RAID" stood there all the time). When the bar is too narrow for all
  it says (the raid state, the whole last fix, the lights with their words, the three buttons), the lights' words
  go first: the three squares stay, and each says its word and what is behind it in its tooltip. They come back
  once there is 40 px to spare, so a figure more in the last fix doesn't switch them on and off. Only then does the
  last fix trim. At 900 px a raid's bar is the raid state, the last fix (whole in the fake raid's snapshot; a long
  one trims), three squares and the buttons; outside a raid the words fit.
- **Rail** (left, 384 px), content by state:
  - *The MAP list* at the top, in every state (owner, 2026-10-08: "there is no way of selecting maps at the moment
    where you do not have quests"; from the maps panel, "A"). The list held every map already, but in the usage log
    (a week) all map picks (dozens) went through Plan's rows and none through it, and after a pick of a map without
    quests the rail stayed on the best suggestion, so nothing in it was about the map on screen. Now, open, it says what
    each map holds: the maps with your quests first, in Plan's order, each with its row's counts at the right in amber
    ("Complete 2 · progress 1"), then a hairline and OTHER MAPS (a heading, not a choice), the rest by name. A map's
    variants drawn with the same artwork are one entry, as Plan counts them (Night Factory is Factory, Ground Zero 21+
    is Ground Zero; until then 17 entries, now 13); a variant on screen shows its map's entry. Closed, it shows the map
    on screen with its counts. It is made anew only when what it says changes, and not while it is open
    (`Rules.MapList`, `SessionSnapshot.AllPlans`). Of the panel's other ways, the other maps by name under Plan's rows
    (B), every map as a folded row (C) and every map by name in place of the list (D) were not taken.
  - *What's New* (owner, 2026-10-07: "how to inform the user about news and changes in the app ... ideally accompanied
    with screenshots"; from two panels, "C", then "Whats new: A"). A window was ruled out (principle 3), and so were
    screenshots bundled in a build (§3: map artwork, renders, portraits and icons) and pictures from the website (a new
    host, §2). So: a card at the top of Plan's rail, under the MAP picker and after the update line, in the panel's
    colour with a `Line` hairline. NEW IN 0.4.0 · PRAETORIAN in the eyebrow style in amber (the version and its
    release's name, below), × in its corner (`E711`, as on a notice),
    up to five lines per version, each a 56×34 picture and the thing's name over one note line, then "Point at one: the
    map shows it." The picture is the thing's map symbol drawn by `MapLegend`, as help's legend rows are, or a small
    drawing of the raid card's clock. **Pointing at a line previews it** on the map, as resting on a map's row in Plan
    does (0.6 s; the line takes the linked tint): its map with the thing staged, the preview label "PREVIEW · NEW IN
    0.4.0 · YOUR EXTRACTS THIS RAID" with "AN EXAMPLE, NOT YOUR RAID", and leaving puts the map back as it was. The
    previews (`replay`, `extracts`, `clock`, `joined`, `leaders`): a made-up raid replayed on Customs, an example extract
    list on Customs, the raid card's own clock with example times on a plate over the dimmed map (`RaidClock`), Gratitude's
    two objectives on one spot on Woods, Streets' Scav Checkpoint where an extract and a transit stand apart. Nothing of
    the player's is shown or changed, and nothing is bundled: every picture and preview is drawn when it shows, from the
    cache, by the app's own code (`MainWindow.WhatsNew`). **When:** at the first start of a version with lines, outside
    raids; the versions newer than the one last seen (`whatsNew.seen` in shturmap.db), newest first, each under its
    own NEW IN line. A first start (no `help.seen` either) shows none: help opens, and the newest version counts as
    seen. After an update from a version before the card, the newest version only (`Rules.WhatsNew.Due`). Never by
    itself in a snapshot or the demo. **Gone:** with ×, or once the first raid since the update is over; help's WHAT'S
    NEW IN 0.4.0 · PRAETORIAN brings the newest version's card back. The lines are docs/whats-new.md, built into the
    app: one section
    per version, "- preview · Name · What it is.", at most five (a test checks the file: known previews, short words). A line about the tour (`tour`, or `tour:5` for a
    chapter a release changed) previews nothing; a click on it opens the tour there (owner, 2026-10-09, T13; *The tour*).
    0.4.0's card names the tour in place of "Symbols stand apart", which was the least a player acts on.
    **Release names** (owner, 2026-10-09: "I would like to have names major releases. The first one would be
    praetorian"). A release that raises the minor or major number gets a name, the owner's, written after its version
    in docs/whats-new.md (`## 0.4.0 · Praetorian`); a patch release keeps its line's name (`Rules.WhatsNew.NameOf`).
    It shows in the card's heading and help's link (in capitals, after the version), in the version line at the foot of
    settings ("Shturmap 0.4.0+… "Praetorian" · installed"), in the release's title on GitHub (`Shturmap 0.4.0
    "Praetorian" (private testing)`, `eng\publish-release.ps1`) and the release notes' first line. Not in tags, file
    names, the installer or the updater: those stay the version alone. The preview label keeps the version only.
  - *Plan*: last raid in one line, with **REPLAY** at its right while the raid can be replayed (the play symbol `E768`
    and the word, in the status words' style, as CLEAR PICKS stands beside NEXT RAID; owner, 2026-10-07: "Replay link:
    ii"; "Map drawing", *The raid replay*); **Next raid**: every map your quests have work on, ranked by what can be
    done there, as a list of rows (owner, 2026-10-09: all of them; until then the planner's best four, so the list
    stayed short above the open card; the rail scrolls, and the card now starts lower with many maps), then the open
    map's card: its line in words ("Complete 7 quests · progress 2 more") with
    COMPLETE, PROGRESS and BRING (keys, items to bring). A row of the list is the map's name and its counts
    ("Complete 5 · progress 1"); the open map's row is marked with a gold bar, and with one suggested map that is
    on screen there is no list (review of 2026-10-04, H4: the open card is some 840 px tall, and the other maps'
    folded cards sat under it, below the fold). **The open card is the map on screen's** (owner, 2026-10-08, with the
    MAP list's "A"; `Rules.PlanList.Open`): its row's card; a map none of your quests is on has no row, and its card
    comes after the rows: the map's name, "None of your quests is on Factory" in muted ink, and its raid
    length and bosses (`PlanCard.CardOnly`). Only with no map on screen yet is it the best suggestion's. Until then a
    map that wasn't suggested left the best suggestion's card open and its row marked. The pen and CLEAR PICKS act on
    the open card's map, so on the map on screen. Each quest row in the card is the quest's name, then a quiet line of what it asks on
    that map in a few words, at most two lines and then "…" ("Ballet Lover" over "Find balletmeister's apartment ·
    Survive and extract"; see §5, "Quest synopsis"). The raid card keeps names only: its objective lines already
    say what to do. COMPLETE and PROGRESS are each in effort order, with a thin hairline where a later group starts
    and no headings (§7, "Plan order"); the raid card stays nearest first. Pointing at another map's row previews
    it on the map; clicking it opens its card and shows it; that click is optional.
    Every row carries enough to compare the maps without opening them (the study log: ten card
    clicks in 4.5 minutes to compare maps): one quest-type glyph per quest, all gold, those to complete first, then
    a hairline, then those to progress, and up to five cells of what to bring. **The open map's row has them too**
    (owner, 2026-10-04: "when you click on one of the icons to highlight a quest, this map is selected and all the
    icons disappear that were currently seen before because the map is now the selected one. This is a cognitive
    dissonance"). Until then the open map's row was its name and counts alone, its card below saying the rest: a
    click on a glyph holds its quest's card and, the row being a button, shows its map, and the glyph clicked was
    gone. With every row the same, what was clicked is still there with its card held beside it, the open map can
    be compared with the others glyph for glyph, and no row changes height when the map changes, so nothing moves
    under the pointer.
    **PROGRESS is not less important** (owner, 2026-10-03: muted PROGRESS rows "suggest less importance", while
    these quests are as much this raid's work). Rows in PROGRESS, in ANY MAP and in the raid card's PROGRESS look
    exactly like COMPLETE rows: gold glyph, ink name, trader portrait and bring cells at full strength. The section
    heading says which, and a PROGRESS row in Plan adds one short line in small muted capitals saying why it only
    progresses ("2 OF 5 OBJECTIVES HERE"; §7, "Raid planner"). Muted keeps one meaning in quest rows: an objective
    done after the raid, at a trader. Resting on a map's row for 0.6 s **previews** its map with its quests on it, labelled
    "PREVIEW · CUSTOMS · CLICK ITS ROW TO PLAN IT"; moving to the next row switches at once, leaving puts the
    shown map back exactly as it was (pan and zoom; fitted where it wasn't drawn yet, not at the camera's default:
    review of 2026-10-09), and a click on the row keeps it. A raid loading ends a preview.
    The picks of the map on screen stay off a preview's map (the same review: each snapshot wrote them into it, and
    into the tour's example).
    **Only a row switches the map** (`PlanList`; review of 2026-10-04, "two clicks, two meanings"). The open map's
    card was one button until then: when its map wasn't the one on screen (another picked in the MAP list), a click
    on a quest's row or its pen inside the card also switched the map. The card is no button now; a click in it is
    a click on what is under the pointer. Resting on it still previews its map while another is on screen. So that
    there is always a row to click, the list is also there for one suggested map while another map is on screen.
  - *Raid*: **the map's Plan card, live** (owner, 2026-10-01: the raid view must not be a different screen with a
    different logic). THIS RAID holds one card in the expanded Plan card's look: the map's name, the same summary
    line ("Complete 5 quests · progress 1 more"), the bosses with their spawn chances, and, right under the map's name,
    **the raid's time as the card's largest figure** (below; until 2026-10-05 a line of its own, "12 min in · 28 min
    left", in ink and the summary's size)
    (owner, 2026-10-04, in the evening: "In the raid we do not care when the start time was and all this info, we
    typically want to see how much time we're in the raid and how much time is left. This is crucial
    information". It replaces the same day's "40 min raid · started 21:02" with the game's time of day, made when
    the review called "N min left" a figure the game never states. The owner wants it back, so it is back, said
    honestly: the time in the raid counts from the raid's start line in the log, the time left is the map's raid
    length from tarkov.dev minus that, the tooltip says so and that a reconnect can throw it off; past the raid's
    length it says "47 min in · past the raid's 40 min", never a time left below one; a Scav, who joins a raid
    under way, gets "7 min in · time left not known"; a map without a raid length in the data gets the time in
    alone (`Rules.RaidTime`). While the raid loads, "40 min raid" stands with the bosses, as in Plan.)
    **The card's head stays at the top of the rail** (owner, 2026-10-08: "the remaining time is crucial information
    and when scrolling down in the quest window it gets pushed outside of view. It should always stay on top"): THIS
    RAID, the map's name with the side, and the readout stand under the MAP list, outside what scrolls, and the rest of
    the card scrolls under them. The card is drawn in two parts that meet without a seam, the head open at its foot and
    the body open at its top, so unscrolled it looks as before; once anything has scrolled under it, the head closes at
    its foot with the card's edge. The rail's hint stands above THIS RAID in a raid. A copy
    of the readout pinned only once the card has scrolled away was considered and not built: it would cover the top of
    what scrolls (the ways out ALL 15 ↓ brings up), and the card would show two clocks.
    **The readout** (owner, 2026-10-05: "The remaining time is a crucial piece of information and should be more
    visible. I would make it a prominent item in the UI and maybe even have a cool animation that is in-line with the
    style of the app. It should not be too crazy though to not steer away the attention through its movement";
    `Controls.RaidClock`). The minutes left stand in 44 px, semi-condensed semibold, in ink: the largest type on the
    card, twice the glance's distances. Beside its foot "MIN LEFT", and at the right "27 MIN IN", muted, both in the
    status words' style. Under them a rule of the raid's length, 4 px: the time gone in the hairline's colour, the time
    left in ink, a mark where now is, and a tick every ten minutes (and at both ends), so it reads as a scale like the
    map's. **The last ten minutes are red** (figure, words, the rule's light part), the colour of danger and what the
    game's own timer does (the owner's screenshots: white at 0:33:47, red at 0:08:34). Where no time left is known
    the figure is the time in the raid, said as "MIN IN": a Scav's ("TIME LEFT NOT KNOWN" at the right, no rule), a
    map without a raid length (no rule), and past the raid's length ("PAST THE RAID'S 40 MIN" in red, the rule all
    gone). The tooltip says how the figures are made, as before. **It doesn't move** (principle 2): at a new minute
    the figure and the rule just change, so it never pulls at the eye from the second monitor (a mark that breathes
    all raid was considered and left out for the reason picks hold still). Until 2026-10-09 the figure decoded at
    each new minute as the big cue's title does and one dark notch ran along what was left of the rule; the owner had
    both removed in the pre-release review. The time left is still the map's raid
    length minus the time since the start line: reading the game's own timer from a screenshot was looked at the
    same day and not built (§2).
    and, when the distances aren't from a fresh screenshot, where they are from. **No loading progress** (owner,
    2026-10-04: "we really don't need the loading progress bar in the upper left, the game already shows a loading
    progress bar"): the line and the segments described next were shown from 2026-10-02 until then, and the
    tracker still keeps the steps for the study log. What was shown: while the raid loads, one quiet
    line under the raid line names the last loading step the game's log reported ("LOADING · MAP", then "MAP
    LOADED", "RAID PREPARED", "RAID CREATED", "PLAYER SPAWNED", "GAME POOLED", "GAME RUNNING"), over six thin
    segments, one per step, each gold only once its own step is in the log (owner, 2026-10-02: loading takes
    60–130 s and showed nothing; and nothing estimated, only what the log says, so a skipped step stays dark and no
    rule creeps by typical times). No time estimate. While it loads, CHECK YOUR KIT follows (the kit reminder, UX
    principle 9), and BRING waits below until the raid starts. Then **the glance**, two rows in
    big type between hairlines: NEXT, the nearest objective with a place (its text, its quest, the distance and
    direction), and EXIT, the nearest extract for your side (and what it takes), never a transit (owner, 2026-10-05:
    "It counts transits as exfils. I would show primarily exfils"; a transit leads to another map, not out; until then
    EXIT was the nearest way out of either kind; `Rules.ExitsNote.Exit`). The game opens only some extracts in each raid,
    by where the player started, and neither its logs nor tarkov.dev's data say which, so at a raid's start the
    nearest is usually not one of the player's (owner, 2026-10-04: say it in the row; a transit is open to everyone and
    gets no note). **Checked or not is said** (owner, 2026-10-05: "Since the app shows the nearest exit upon placing a
    marker it should also be visible if the exfils [are] not verified yet"; `Rules.ExitsNote`). Until a screenshot
    showed the game's own list (§2; *The extract list*, below) the row says "NEAREST · NOT CHECKED AGAINST YOUR
    LIST", muted, its tooltip saying how to check; with the reading unticked, or on a Windows without text
    recognition, the words of 2026-10-04, "NEAREST · CHECK YOUR LIST IN GAME". Once the list was read, EXIT is the
    nearest extract that is on it, never one it leaves out, and says "ON YOUR LIST THIS RAID" in
    the way out's colour, or "ON YOUR LIST · ??? IN GAME" for one the game marks "??:??:??". Under the
    direction, a small gold "ALL 15 ↓" brings the whole list of ways out, which stands under the card, to the top of
    the rail (the review of 2026-10-04, H5: with several quests that list is a long way down, and it is the one to
    read when the nearest exit isn't the player's). **OR** (owner, 2026-10-05: "also show the next one you sure is
    open and where you don't need to bring extra things or do extra things - where you can simply exfil"): under EXIT,
    a smaller row with the nearest extract the list names without "??:??:??" that takes nothing (no item, money,
    flare, climbing gear, switch or second player: an empty `ExtractRules.Needs`), "NOTHING NEEDED · ON YOUR LIST", in
    its kind's colour, with its distance and direction (`Rules.ExitsNote.Plain`). Only once the list was read (nothing
    is sure to be open before), never a transit, and not where EXIT is that one already. Both notes take a second line
    where the direction beside them leaves too little room, with "…" only past that (review of 2026-10-09: a Scav's
    "BEHIND-LEFT" cut EXIT's note off mid-word, without an ellipsis). The glance's rows hide with
    `FallbackValue=Collapsed`: x:Bind didn't call `ShownIfSet` again when its value went to null, and an emptied row
    stood on with what it said before. In a raid the app gets
    glances, median 3.9 s in the study log, and these are what a glance is for. Then COMPLETE, PROGRESS and BRING
    as in Plan, except that each quest line carries its objectives on this map under it: text, the key or item it
    needs (gold), and on the right the distance, direction and floor hint; "anywhere" for kills and finds with no
    fixed place, "after the raid" (muted) for a hand-over of its own (one that gives what another line gets is a
    handshake after that line's words, "Quest cards", *Hand-overs*). The text is the objective in a few words, in these lines
    and in NEXT: its phrase from the quest's synopsis ("Mark Stryker" for "Locate and mark the Stryker with an
    MS2000 Marker"; §5, "Quest synopsis"; `Planning.ObjectiveSynopses`), since the card is read in seconds; the
    quest's own card keeps tarkov.dev's sentence, and so do these lines when the data isn't in English (review of
    2026-10-04, C1: three full sentences were the card's longest part). The need stays on every line, also when
    three lines in a row say "Bring: MS2000 Marker": each place needs one of its own (owner, 2026-10-04: "keep the
    item with each sub-item so it's clear that one is needed"; saying it once under the quest's name was tried and
    declined). Objectives inside a quest go nearest first, and quests
    by their nearest objective, so the top of the list is still where to go next. An objective the player ticked
    as done ("Quest cards", *Ticks*) comes after the quest's open lines, muted, with "DONE" where its distance was
    and without its needs; NEXT and the guide line pass it by, and a quest with nothing open on this map isn't
    listed. Below the card, extracts and
    transits for your side, each with what it takes to leave there (see "Extract requirements").
    **The extract list** (owner, 2026-10-05). Under the heading one line in the note style says where things stand:
    "Not checked against your list yet. Screenshot the game's extract list (raid start, or O twice) to check.", then
    "Your list this raid: 6 of 12 extracts (screenshot at 16:40)." (or that this Windows has no text recognition
    language; nothing with the reading
    unticked). Once read, each extract's small line adds "· ON YOUR LIST" or "· ON YOUR LIST · ??? IN GAME" and takes
    the way out's colour; one the list leaves out says "· NOT ON YOUR LIST", its name and distance muted, without
    what it would take, and comes after everything else. The list puts extracts first, nearest first, then the
    transits, then the extracts the list left out (owner, 2026-10-05: "I would show primarily exfils"). A
    notice says it once ("Your extract list is read: 6 extracts for this raid"). What a raid's screenshots name adds
    up: a later list adds extracts and renews the "???" marks of the ones it names, and never takes one away (a
    reading that missed a row must not close an exit). A transit is never said to be on or off the list: it is open
    to everyone, and its rows aren't matched. **What counts as the list**: rows that name two or more of the map's
    extracts for the player's side; one named extract counts only under the header "Find an extraction point", since
    the box the game shows while the player stands in an exit ("Stay in the extraction point") has the same green bar
    and one row, and taking it for the list would call every other exit closed (so in another game language a list
    of one exit isn't taken). Names are matched in the game's language and in English, letter by letter with what a
    reader confuses folded together (the slashed zero comes back as "Ø", 1 as "I"), one letter in five may be off,
    and a row that fits two exits equally names neither (`ExitList.Match`). Every map is unchecked again when the
    raid ends or a transit loads the next map. A way out wears
    the colour of its kind here as on the map, in EXIT's label and distance and in each row's distance: a PMC
    extract green, a Scav's teal, one for both sides khaki, a transit violet (2026-10-04: every way out was green
    in the rail, the PMC extract's colour on the map). ANY MAP closes the rail in both states.
  - *The raid's map is not the map on screen* (review of 2026-10-04: a map picked in the MAP list during a raid
    took the raid with it; the next position, the status bar, the raid card and RAID OVER went to the picked map).
    The raid, its card, the status words, the cues and the last-raid line are about the raid's own map, as the
    game's log names it (the scene, then the location; `SessionSnapshot.RaidMap`). The MAP list stays usable in a
    raid, as a look: the map shows the picked map without a "you", the rail stays the raid's, and the next position
    is plotted on the raid's map and brings it back on screen, as the raid's end does (2026-10-06). A label at the top of the map says so for as long
    as it lasts, in the preview label's style: "LOOKING AT WOODS · THE RAID IS ON CUSTOMS · YOUR NEXT POSITION SHOWS
    IT AGAIN". A raid on a map the data doesn't know names no map ("IN RAID · PMC · 12 MIN", "LOADING"; the raid
    card is titled "MAP NOT KNOWN") and plots no position, with a notice saying why: the map on screen is
    never taken for the raid's. A map tarkov.dev gives no scene for (Ground Zero 21+) is named by the match
    setup's or the transit line's location a moment after the scene line, and its RAID LOADING cue comes with that
    line.
  - *Scav raid* (owner, 2026-10-01: a Scav needs a different view): the same card, with SCAV beside the map's
    name (PMC in a PMC raid; nothing when the logs can't tell). Quest objectives only count for the PMC, but items
    found in raid count whoever found them, so the card's summary is "Find items for 4 quests" and its one
    section is FIND IN RAID: the items your active quests need found in raid (find/hand-over objectives marked
    found-in-raid), as BRING rows with icon, count and quests, the ones lying loose on this map first ("Loose
    here · 3 spots"; pointing at a row draws the spots), at most eight and one line for the rest. One note line
    says why. The raid line says "joined 21:02" in place of the raid's length and start (a Scav joins under way; the logs don't
    say how long the raid has run or how long is left). The
    map draws no quest objectives, and in any raid only your side's extracts. ANY MAP is hidden (a Scav's kills
    don't count). When the raid starts as a Scav, the SCAV RAID cue says so (a notice said it a third time, beside
    the cue and the note, until the review of 2026-10-09); a server raid's setup tells it while loading already, and
    then no PMC kit is shown (the kit reminder, UX principle 9). The side is set from the setup then,
    and decided again at the raid start.
    How the side is known: the menu loads the PMC profile; a server-hosted raid's match-setup line names the
    joining profile (same id: PMC, another: Scav); a raid that starts without "GameStarting" is a Scav joining
    under way. PvE raids can be either: server-hosted ones log the match setup like PvP (seen on several days),
    locally hosted ones (seen on some days, on one of them beside server-hosted ones) log neither and start with a
    zero-length "GameStarting". Those stay unknown and get the PMC view; the side tag is then a switch (PMC ⇄
    SCAV) that holds for the raid. No local Scav raid has been seen in a log yet; if one shows a line that tells,
    it replaces the switch.
  - *No game* (owner, 2026-10-03: "Yes, build the fallback 1-5"; the one 30 s notice was easy to miss, and a game
    Shturmap couldn't find left no way to point it there). While discovery finds no game, one line stands where the
    Plan card would be, for as long as that lasts: "NO GAME FOUND ON THIS PC", **CHOOSE GAME FOLDER…**, and "Or
    browse the maps above. Quests and raids follow the game once it's found; Shturmap keeps looking."
    (`GameStateLine`). A game found without log sessions says "THE GAME
    HASN'T RUN ON THIS PC YET", where it was found, and offers the same choice (a wrong install may have been
    found). NEXT RAID, its cards and the "none of your quests" hint step aside: without the game's logs there are
    no quests to plan, and nothing quest-like is shown. The map browses as always (extracts, transits, bosses,
    spawns, landmarks, hazards), and the mode is chosen by hand (status bar, above). No notice says it a second time.
    - **Choose game folder…** opens Windows' folder picker. The folder counts if discovery accepts it: the game's
      build folder with `EscapeFromTarkov.exe` or a `Logs` folder with sessions, or the folder above it (Steam's
      layout). Otherwise a notice says why ("That folder doesn't hold Escape from Tarkov: …") and nothing changes. A
      chosen folder is saved (`installFolder`) and wins over discovery. It is followed at once, without a restart:
      the old logs let go, the new ones read for quest history and followed live, the game's settings read again
      (all of it off the window's thread, as for FIND AUTOMATICALLY: reading every log session takes a while);
      the screenshot folder is the user's Documents one either way (§2's boundary unchanged). A network folder that
      holds the game works the same, but isn't offered as a feature (the laptop beside the game PC is undecided).
    - **It keeps looking.** Discovery runs again every 30 s for as long as Shturmap runs (registry and file checks
      only). While the game or its logs aren't found, a game installed or first started later is followed by itself,
      and a notice says so once ("Found Escape from Tarkov in …: quests and raids follow the game now"). While a
      game found automatically is followed, the same look notices another copy of the game started since (a test
      server, the other launcher's install): discovery's rule is that the newest log session wins, so once another
      install has a newer one, its logs are followed instead, read for quest history, and a notice says what was
      seen, "Newer game logs in …: quests and raids follow that game now" (`GameSession.FollowsInstead`; review of
      2026-10-04, A20: discovery decided once, at the start, and the install it found then was followed for the
      whole run). A followed game is never let go for nothing: a look that finds no game or no logs changes nothing.
      A folder the player chose is never switched away from (below: the hint).
    - **Find automatically** (owner, 2026-10-04, asked whether a folder chosen by mistake leaves no way back: "sounds
      good"). A chosen folder that holds *a* game, just not the one played, wins over discovery for good, and
      CHOOSE… alone can't undo that. Settings' APP AND DATA row says where the folder comes from: "GAME FOLDER: … (chosen
      by you)", "(found automatically)" or "NOT FOUND". While a folder is saved (even one that no longer holds the
      game), **FIND AUTOMATICALLY** stands beside CHOOSE…: it forgets `installFolder` and finds the game again at
      once, followed live as after a choice, and a notice says what it found ("Found Escape from Tarkov in …", "…
      hasn't run on this PC yet", or "No game found on this PC…"). When the chosen folder holds the game but another
      install discovery found has a newer log session, settings say so quietly under the row, "Newer game logs in …
      · FIND AUTOMATICALLY", and so does the LOGS light's tooltip (`GameFolder`). Nothing pops up and nothing switches
      by itself: the choice was the player's. Session dates are read from the logs' folder names, so the hint is a
      fact, not a guess about which install is played; the 30 s look-again keeps it current.
    - Developer switch `--no-game` (developer builds only): discovery looks only at a folder chosen in the session,
      with an app folder of its own so a choice never sticks in real settings; with `--fake-game` the fake game isn't
      found either, until chosen. The developer view's "No game" trigger does the same at runtime and keeps the
      fake game's screenshots, and its script step `choose <folder> | game | auto` chooses a folder, or finds the
      game automatically again (with `--no-game` that is the no-game state).
- **Map** (rest): artwork, quest markers with type glyphs, extracts, transits, spawn zones (see "Map drawing"),
  player, trail, guide line to the nearest place of the picks. Map controls bottom-right (follow my position, show my
  position, show the whole map, zoom in, zoom out), with the floor picker above them on maps with floors;
  one-line notices top-centre; bottom-left a WIKI MAP link (the map's interactive map on the EFT wiki, for loot,
  containers and the rest Shturmap doesn't draw) above the attribution. A map's scene goes into the view once its
  artwork has arrived, and a snapshot is written only into the scene of its own map (`SceneGate`; review of
  2026-10-04: the map counted as shown from the moment it was asked for, so a snapshot that came before the artwork
  put the new map's markers and position onto the old map's picture). Until then the map before stays as it was.
- **Follow my position** (owner, 2026-10-03: "It should be a toggle in the map view and the camera should smooth
  scroll to the updated player position"; it replaces 2026-10-01's "a new fix never moves the view", principle 8):
  a toggle at the top of the map controls, the same 36 px square-cornered frame, its own symbol `E759`, the tooltip
  "Follow my position (Shift+F)"; on, its symbol and frame are amber, the app's colour for "on" (2026-10-04: it was
  the picks' cyan, which then had two meanings; "Design system", one meaning per colour). Off by default, and kept
  between runs (`followPosition` in shturmap.db).
  - **On**, each new position glides into view over 2.4 s, eased in and out, at the current zoom; the
    floor follows the height as always, and the position pings as it arrives. Turning it on glides to the last
    position at once. **Slow, and with room ahead** (owner, 2026-10-04: "that smooth panning should be way slower.
    Also, it should not dead-center on the player, but rather show more of the map in the area where the player is
    looking. Find a good ratio here"). It was 0.5 s, eased out only, so most of the way was done in the first
    moment and it read as a jump; now it sets off gently, travels and settles (`Camera.PanAt`, `MapView.FollowPan`).
    And the view's middle lies a sixth of the view ahead of the player, the way they face (`FollowState.Lead`), so
    the player stands a third in from the edge behind and two thirds of the view lie ahead: the rule of thirds. A
    quarter was the other candidate; it leaves three quarters ahead but puts the player 25 % from the edge, with
    little to see beside and behind. The lead is taken only while the facing is still shown (45 s, `Facing.Fresh`)
    and the screenshot's name gave one: an older position goes in the middle, since nothing says any more which
    way the player looks. Zooming while on the player keeps this place, and F and the button use it too. With Windows' animation effects off the view jumps there instead. While it glides the edge
    badge waits (the position is on its way into view) and no notice says the position is out of view.
  - **Only the toggle turns it off** (owner, 2026-10-04: "when you have the follow on you should be able to drag and
    zoom. When a position is updated, it should center back to the player but at the current zoom level"; until
    then a drag or showing the whole map switched following off, also for later raids and starts, so it could never
    stay on). Dragging the map or showing the whole map (0, the button) takes the view for now; the next position
    glides it back onto the player, at the zoom the view then has. Zooming (the wheel, double-click, + / −) keeps
    the player in the middle while the view is on the player, and is free, at the cursor, once the view was dragged
    away (`FollowState.Away`). F and the show-my-position button centre at once, as before, and put the view back on
    the player. Pointing at a quest or holding a card never moves the view. While the view is away and the position
    is out of it, the edge badge points to it, as without following.
  - A map opened while following (a raid loading) is fitted first, then centred on the player once there is a
    position. Snapshot and demo runs leave the saved choice alone; `--follow` turns it on from the start (snapshots,
    the dev view).
  - The glide draws through the map's own GPU surface and paint, as every other frame; each frame works out its own
    point on the glide as it is drawn. Only while it glides, a frame timer asks for frames at the system timer's pace
    (about 64 a second): the map's 16 ms animation timer and the Rendering event each gave only about 30 a second
    (measured with the dev view, 2026-10-03).
- **Help** (F1 or `?`): one panel with TAKE THE TOUR and WHAT'S NEW IN 0.4.0 under its title (the tour from its start; the What's New card again), how it works with SHOW ME after the paragraphs a chapter of the tour shows (it opens the tour there; owner, 2026-10-09, T11), the shortcuts, the quest-type legend and the symbols on the map
  shown, the others behind a link (see "Map drawing", "Legend"), what Shturmap reads and that nothing is sent unless the player sends a report or allows
  crash reports, and HELP AND FEEDBACK: a line pointing to the feedback button, and COPY DIAGNOSTICS (§8,
  "Diagnostics"). Until 2026-10-09 it opened once by itself on first run, outside a raid; since then the tour does,
  in its place, and help opens only when asked for (a snapshot run still opens it for its picture). A step into a raid (it loads, it starts) closes the panel, since it stays open while the game
  has the focus and lay over the raid card; if it had opened by itself, it isn't counted as seen and comes back
  after the raid (`WhileInRaid`; review of 2026-10-04, H9).
- **The tour** (owner, 2026-10-09: "a proper first-time-opening tour of the app. This one should then later be
  accessible through the help menu itself. The tour should give the player a proper onboarding experience with the
  most important features, how everything works and what the most important aspects of the UI are. Here we can be a
  bit more flashy with nice animations"; "We need to keep in mind to update the onboarding with future updates"; from
  a panel of four forms and thirteen decisions, all taken as recommended: the briefing (A) told through an example raid
  (B), with first-time lines (E); `MainWindow.Tour`, `Rules.Tour`). Seven chapters over the real window, about a minute
  and a half: WHAT IT READS (what is read, what is never done, the risk in the README's words; review of 2026-10-09:
  its first title used a word §2 keeps off Shturmap), IT FOLLOWS THE GAME (Plan
  and Raid), NEXT RAID (Plan's rows, one resting under a drawn pointer previews its map), PICK AND POINT (an example
  quest picked, another pointed at), YOUR SCREENSHOT KEY (the one habit Shturmap needs: the key pressed large, the file
  name decoding, the position pinging; then the extract list as its own cause and effect: an example of the game's
  list, in Shturmap's look, decodes in the map's top right under "IN THE GAME: O TWICE, THEN PRTSC", a press reads
  it, the list goes, and the view takes in its extracts as they light up one after another while the rest go hollow), IN THE RAID (RAID LOADING with an example kit, then the raid
  card's head with example values) and HELP AND FEEDBACK (the story chapters aren't shown yet, anything else missing goes through the feedback button,
  F1; the three buttons named; owner, 2026-10-09: "GOOD TO KNOW" wasn't "really expressive").
  - **How it looks.** The window dims (ground at 86 %) except where a chapter's parts are cut out, each framed with the
    map sheet's corner marks in amber; a band at the map's foot, where the replay's band stands, on the ground colour
    with nothing showing through (review of 2026-10-09: at 94 % the map's labels read through its words, and through
    the stage's plate, opaque too), holds TOUR · 3 OF 7,
    the chapter's title, decoding as the cue's does, its lines, ← BACK, NEXT →, ESC ENDS THE TOUR and a tick per
    chapter; a stage's plate (the READS / NEVER lists, the two states, the key, the example raid card) stands in the
    middle of the map above it. Motion: "Design system", *Motion*.
  - **How the frames move** (owner, 2026-10-09: "sometimes they transition into nothingness, like from section 3 to
    section 4"; `Rules.TourFrames`). From one set of parts to the next, each new part (the largest first) takes the
    nearest frame on screen and the frame glides to it; a frame left over glides into the new part nearest it, and a
    new part left over grows out of the frame nearest it, so nothing shrinks into a point while there is a part to go
    to. A glide starts from where the frames are drawn at that moment, also mid-glide. With no frame on screen the new
    parts fade in, and a chapter without parts lets the frames fade out under the dim. The dim leaves out the union
    of the frames, so two that cross while they glide stay clear where they overlap (until then the overlap was dimmed
    again, the dim's holes being cut even-odd). Parts that touch are one frame (the three buttons).
  - **Examples, never the player's.** What a chapter stages is drawn from the cache by the app's own code, as What's
    New's previews are: no picture, render or video is bundled (§3), and the map's label says "PREVIEW · THE TOUR ·
    CUSTOMS · AN EXAMPLE, NOT YOUR RAID". The example is the same for everyone: Customs, which every player knows, with
    up to three of its early quests that have places on the map; a cue it shows says "An example, not your raid.".
    Nothing of the player's changes: no pick, no tick, no setting but the tour's own. NEXT RAID frames the player's own
    rows and lets one preview its map as resting on it does; without rows (no game) it frames the MAP list. When the
    tour ends, the map on screen is back as it was, view and all; at a first start, when the tour opens before any map
    is drawn, it comes back fitted (review of 2026-10-09: it came back at the camera's default, zoom 1 on the map's
    origin, from chapter 7 on).
    A glide a chapter starts stops when its map goes (the same review: Esc during YOUR SCREENSHOT KEY's glide went on
    moving the map that came back).
    A chapter frames what it stages above the band with 90 DIP around it, room for the labels the map writes beside
    its symbols (the same review: 40 and 60 cut them at the map's edge, and IN THE RAID, fitted, ran under the band),
    and 150 on the right, the map's label room (owner, 2026-10-09: 90 still cut PICK AND POINT's picked quest there;
    "Map drawing", *Room for labels on the right*).
    IN THE RAID's example card says NEXT in a few words, as the raid card does ("Get valuable folder";
    `Planning.ObjectiveSynopses`; until then tarkov.dev's sentence, cut), and its clock reads 13 MIN LEFT of the map's
    own raid length (Customs: 35 min in tarkov.dev's data of October 2026, so 22 MIN IN).
  - **When.** By itself once, at a first start (neither it nor help seen on this PC), when the data is there and
    outside a raid, in help's place; a raid starting closes it, and it comes back at its chapter once the raid is over
    (as help did, `WhileInRaid`). From help's TAKE THE TOUR (its start) and SHOW ME (a chapter), and from a What's New
    line about it (below); never in a raid, these ways too (review of 2026-10-09: they opened it over the raid card):
    TAKE THE TOUR is hidden while a raid loads or runs, and a SHOW ME or a What's New line then opens nothing and says
    "The tour waits until the raid is over.". Ending it, at its last chapter or with Esc, counts it and help as seen,
    and a notice says where it stays ("The tour stays in help: F1, then TAKE THE TOUR."). Never in the website demo;
    "--tour <n>" opens a chapter for a snapshot (`tools\fake-raid.ps1 -PlanOnly -Tour <n>`).
  - **Keys and clicks.** → or Space (or Enter) the next chapter, ← the one before, Esc ends it; every other key does
    nothing while it is up, and a click outside its band does nothing (the window is covered, by the owner's exception
    to principle 3).
  - **Words** live in docs/tour.md, built into the app: per chapter its stage, the parts it frames (their `x:Name`s in
    MainWindow.xaml), its title, at most two lines and its stage's own words. `TourTests` fail when a part it names is
    gone from the window, a stage isn't one the app draws, or a title or line is longer than the band.
  - **Keeping it current** (owner, 2026-10-09). A change to something a chapter shows changes the chapter in the same
    commit. Its drawing is the app's own, so a new symbol, card or cue shows in the tour by itself; what can go stale is
    its words and what it frames, and the test catches the second. A release that changed a chapter says so in What's
    New with a line `tour:N` ("- tour:5 · …"), which opens the tour at that chapter when clicked; pointing at such a line
    previews nothing. Before a release, the chapters are looked at (`fake-raid.ps1 -PlanOnly -Tour 1` … `7`).
  - **First-time lines** (the panel's E). The habit is needed in the raid, long after the tour, so after a first start
    the first raid's RAID LOADING cue carries one line under its kit, "FIRST RAID WITH SHTURMAP · PRESS PRTSC ONCE YOU'RE
    IN" (the key as the game's settings name it), and the first position plotted gets a notice, "That's you, from your
    screenshot's name. Each new one moves you." Each once; a player who used Shturmap before the tour never sees them
    (they are armed only by a first start: `firstRaid`, `firstFix` in shturmap.db), so their words stay true.
- **Settings** (the gear, Ctrl+,): SETTINGS, then PREFERENCES: "Keep a study log" (developer builds only), "Language"
  (AUTOMATIC, then each language Shturmap is translated into, in its own name: ENGLISH, DEUTSCH; developer builds also
  list the ones still in translation, "DEUTSCH · IN TRANSLATION", to try them; a choice applies at once, and under it
  one line says what Automatic comes to and why: "Automatic: English, from the game.", "…, from Windows.", or that
  neither the game's nor Windows' language is translated yet; §8, "The app's own language"), "Delete
  position screenshots" (a tick, off unless ticked, with what goes and what stays; §2), "Read the extract list from
  screenshots" (a tick, on unless unticked, with what is looked at and that nothing of the picture is kept or sent;
  §2), "Crash reports" (ASK AFTER A
  CRASH · ALWAYS SEND · NEVER) with what a crash report holds, "Updates" (AUTOMATIC · TELL ME ONLY · OFF, or "not
  available in this build"; an installed Shturmap whose updater couldn't start says that instead, with where the
  reason is and that the Setup repairs it, `Distribution.NoUpdatesText`); then APP AND DATA: LOG FOLDER, PRIVACY and LICENCES, in an installed build a muted
  UNINSTALL SHTURMAP… with its question; at the foot the version and the kind of build (§8, "Study log",
  "Reports", "Distribution"). It never opens by itself; closing it drops an unanswered uninstall question.
  Split from help on 2026-10-03 (see "Status bar"); the blocks moved as they were.
- **Report dialog** (§8, "Reports"): over the whole window, square, in the card's colours: PROBLEM | IDEA, the text,
  an optional contact, "Include diagnostics" with SHOW WHAT'S SENT, one line on where it goes with a Privacy link,
  CANCEL and SEND.
- **After a crash**: one question under the notices, until answered: SEND · DON'T SEND · ALWAYS SEND · WHAT'S SENT;
  closing it asks again at the next start; once sent, ADD A NOTE opens the Report dialog. It lies over the map and
  waits for a click, so it is shown outside raids only: while a raid loads or runs it steps aside, unanswered, and
  is back when the raid is over (`WhileInRaid`; review of 2026-10-04, H9).

### Map drawing

Rules from a cartography review of the map (2026-10-02; the owner approved its twelve changes, with the change to
spawns below).

- **Four levels, the rule for every symbol.** A reader takes in the map in order of importance; each level differs
  from the next in size, contrast or colour, and there are few things at the top.

  | level | members | treatment |
  | --- | --- | --- |
  | 1 | the player; the picked quests, the guide line and plate; the raid replay's pen while it plays | own hues (sand, the picks' colours), rings, drawn last, never faded |
  | 2 | quest objectives; extracts and transits for your side | amber discs with glyphs (10 px); 15 px triangles and diamonds; labels in ink; the extracts on your list this raid lit, their names in bold |
  | 3 | boss, sniper and Scav zones | one marker per zone; Scav zones unlabelled |
  | 4 | the artwork, its names, the sheet grid; locks, switches, hazards, container dots | receded; names, locks and switches thinned with zoom |

  Extracts and transits are as large as the boss marker (15 px across), no longer smaller. A new symbol gets a
  level, a shape no other symbol uses, a colour from the palette (no new hues) and the collar.
  Where symbols share a place, the one that matters more lies on top: markers are drawn in the order of their
  labels' priority, least first (locks and switches, then Scav and sniper zones, ways out, quests, bosses, then
  what is selected). A Scav zone's ring used to be drawn over the boss's octagon of the same spawn zone, which
  then read as a red ring (the review of 2026-10-04, C6).
  Symbols that would cover each other are **set apart, on leaders** (below, "Symbols that cover each other"); the
  receded ones, locks, switches and Scav zones' rings, only **side by side** with their own rank
  (`MapRenderer.SideBySide`; the same review: on Streets a transit's diamond lay over an extract's triangle at Scav
  Checkpoint, and one quest's disc over another's; until 2026-10-06 every symbol was set side by side). Each of such
  a pair is moved by half of what is missing for both to show, their collars touching, and never further than its
  own width, along the line between their true places, left and right where they share one, the earlier in the data
  on the left; so zooming in draws them apart and each comes back to its own. Places of one objective still merge
  into one marker with their count. Everything follows the symbol where it is drawn: the pointer finds it there,
  its badges and its label stand by it, the guide line ends on it, and a place counts as in view for the edge
  chevrons where its symbol is. The distance on the guide's plate stays the place's own. A symbol set off its place
  may put its name a line further down than "below", under its neighbour's: two names read as a pair's caption
  ("Scav Checkpoint" over "Transit to The Lab (Dark)").

- **Symbols that cover each other** (owner, 2026-10-05: "Can there something be done about overlapping icons … Especially
  for selected Quest icons"; chosen from a panel of six ways drawn by the real map on three Streets spots: "Go for F").
  - **Nothing grows.** A pick and whatever is pointed at keep their rest size; colour, the pick's ring and the
    pointer's pulse say it, and the ring and the pulse lie under every symbol (`MapRenderer.DrawUnderlay`). Before, a
    pick grew to 14 px with a ring reaching 7.5 px further and a pointed-at quest to 12 px, over neighbours set
    side by side for 10, so the symbol that mattered most covered the ones around it. Symbols are set apart (below)
    by the sizes they are drawn at.
  - **Set apart, on leaders, without a click** (owner, 2026-10-06: "learning from the ggrepel ggplot package.
    Whenever there is overplotting going on, it automatically draws a line between the datapoint and its annotation …
    it should not be the case that I need to interact with the map with the mouse in order to see the spots
    properly"; chosen from a panel of five ways drawn by the real map on four Streets spots: "Overlap: D";
    `MapRenderer.Repel`). Quests, bosses, extracts and transits that would cover each other are pushed clear of each
    other and pulled back toward their places, as ggrepel sets labels apart: where ranks differ, the lesser one
    yields and the one that matters more keeps its place (bosses, quests, ways out); of one rank, both yield alike.
    No symbol may stand on another's place either (ggrepel's point padding, 7 px beyond its own half width), so where
    two share a spot both step off it. A symbol that has to leave its place stands at least 10 px beyond its edge
    from it and is joined to it by a **leader**: a hairline in the symbol's colour on a dark band, with a dot on a
    dark rim where it belongs (the opened stack's hairline of 2026-10-05, now always there). One only nudged (by
    less than about half its width) keeps to its place without a leader, and as the view zooms in and the places
    draw apart, each symbol comes back to its own and its leader goes. A corner badge (floor, count, OPT, "?")
    counts in its symbol's width, so no neighbour is pushed under it. Leaders lie under every symbol and step back
    with their symbols while something else is pointed at; no label covers a leader or its dot; pointing at the
    dot points at its symbol. **The player is a symbol too** (owner, 2026-10-06: "keep in mind that the player icon is
    also an icon - if you are right next to a objective the player icon is drawn on top of the quest icon"): level 1,
    it never moves, and no symbol stands under its ring or, once the position is a minute old, its age tag; one beside
    you steps off on a leader to its place, as from any symbol that matters more (`MapRenderer.PlayerObstacles`). The
    facing cone and the glow are see-through and hide nothing. Positions turn only on the places, where the player
    is, the zoom and the symbols' sizes and ranks at rest, never on picks or the pointer, so nothing moves when
    something is picked or pointed at, and a new position moves only what stood in its way. Locks, switches and
    Scav zones' rings take no part (their leaders were a web of ink between the quests in the panel): they stand side
    by side among themselves, under the rest. A pan moves every place alike, so the repel's offsets are kept while
    the same symbols are in view at the same zoom (`MapScene.LastRepel`): it is most of a layout's work (3 to 6 ms
    for a 1600 × 1000 view of Streets with every quest active), and a pan lays the map out for every frame.
  - Until 2026-10-06 **a stack opened where the pointer rested** (`MapScene.FanAt`): resting 0.35 s on a symbol
    another lay well over put every symbol touching it on a ring around its place, on a dark plate, each on a
    hairline to its own place. It needed the mouse, so it went with the leaders, which leave nothing stacked.
  - Also drawn and rejected (the panels, `shturmap-cli overlap-panel` and `repel-panel`, removed with the choices):
    the upper symbol faint where it lies over another (muddy ghosts of glyphs); neighbours stepping aside for a pick
    or what is pointed at (symbols moving whenever the pointer moves); repelled with only the lesser symbol leaving
    (its dot often lay under the symbol that stayed); every place kept in sight but with leaders as short as the
    room allows (stubs between symbols, no lines to follow); and the leaders in ink rather than the symbol's colour.
- **The player's extracts this raid** (owner, 2026-10-05: "update the map accordingly, highlighting the open exfils").
  Once a screenshot showed the game's extract list (§2), three steps:
  - **On the list: lit** (owner, 2026-10-05, the same evening: "The exits that are verified should be highlighted
    better on the map so it can be seen that they are active"; `MapScene.ExitsListed`). A steady glow in the
    extract's own colour around its solid triangle, about 2.8 times its size across, strongest near the symbol and
    gone at its edge, drawn under every symbol so it covers none; its name in the bold label face, in ink, placed with
    the bosses' names ahead of other ways out and quests. A glow because the other ways to say it are taken: a ring is
    a pick's, a pulse the pointer's, a new hue isn't allowed. It never moves (principle 2). Until then the listed
    extracts were the solid triangles they always were, found only by the others being hollow, and didn't read as
    "active".
  - **On the list but marked "??:??:??"** (it may be closed, or it needs something): solid, not lit, with the "?" badge
    at its lower left that a possible location carries: maybe here, maybe open (`MapScene.ExitsUnsure`).
  - **Left out: hollow**: its outline in its colour on the collar's dark, its name in muted instead of ink
    (`MapScene.ExitsNotListed`). Nothing is hidden: a hollow extract is still a place on the map, and the list could
    have been read wrong.
  Transits are drawn as ever. Until a list was read, and outside raids, every extract is solid. The legend has a row
  for the lit ones and one for the other two, each shown once a list was read.
- **Stepping back while a quest is highlighted** (owner, 2026-10-03: at 28 % the other markers "can be barely made
  out anymore, but are still pretty important", "especially relevant in the raid view"; `MapRenderer.StepBackOf`).
  What is pointed at, and the picks, stay at full strength; the rest steps back by kind, never out of sight, and less
  in a raid. Picks alone step nothing back (owner, 2026-10-03, "Picks"): only pointing does, for as long as it lasts.

  | kind | planning | in a raid |
  | --- | --- | --- |
  | your side's extracts and transits, bosses | full strength (labels 70 %) | full strength |
  | other quests' markers and their zones | 62 % (labels 45 %) | 80 % (labels 60 %) |
  | Scav and sniper zones, locks and switches, hazard areas | 60 % (labels 50 %) | 75 % (labels 60 %) |

  A zone steps back as far as its kind's markers, and a picked quest's doors (the locks of the keys it needs) not at
  all, like the pick they belong to (`MapRenderer.ZoneStrength`, `StepBackOf`). Until the review of 2026-10-04 other
  quests' zones fell to about 35 % whatever this table said, hazard areas to about 55 % (they weren't in it), and a
  pick's doors stepped back like any lock.
  Labels step back further than symbols, so the highlighted quest's names stand out without hiding where everything
  else is. Stepping back by colour (greying) was compared and dropped: a greyed quest marker reads as done (grey
  means done or after the raid). The candidates (all at 60 %, greyed, by kind) were
  compared side by side on Customs and Streets, in a raid and in Plan.

- **Optional objectives** (owner, 2026-10-03: they "might still be very relevant for a quest"). tarkov.dev marks 60
  of 1,418 objectives optional; 45 have places on a map (most on Customs, 15, and Streets, 11: Abandoned Cargo's
  seven cargos, Pyramid Scheme's ten). Their markers carry a small "OPT" badge at the upper left, in the badge style
  of the count (dark plate, the marker's colour), and the quest card and the raid card say "(optional)" after the
  objective. Words, because no shape, colour or ring is free to mean "optional": grey is done, a dashed ring is an
  old position, and a dotted ring, also tried, read like either (the hollow ring was a possible place until
  2026-10-04, see "Possible places" below). A label
  suffix was tried too; it disappears with the label.

- **Landmarks from tarkov.dev's data** (owner, 2026-10-03: "Do B and A"; B, landmarks, came with the question of
  backgrounds for The Lab, Labyrinth and Icebreaker, and holds on every map). All level 4, all real positions from
  the maps payload, nothing inferred:
  - **Locks**: a door's or a car trunk's (both need a key): a padlock (Segoe Fluent `E72E`) on the dark collar, no
    plate, labelled with the key's short name as printed on the key ("TGL MO", "Dorm 114", "RB-PKPM"; the full name
    when it has none) and "needs power" where the data says so. The key glyph `E8D7` stays the key itself (BRING,
    need cells); the padlock is where it opens. Pointing at a key (its BRING row, its row on a quest card, its
    need cell beside a quest's name, the gold "Key: …" line under a raid objective that names just that key) lights
    every lock it opens, at any zoom, and shows where it lies loose; pointing at a padlock opens its key's card.
    Leaving a need cell or the gold line returns to the quest's row around it ("Visual language", linked
    highlight; the review of 2026-10-04: before, a linked element inside a linked row lost the row's highlight
    when the pointer left it, so these didn't link). The key's item card still lights nothing by itself.
    Container locks would mark containers; the data has none.
    A quest brings its doors along (owner, 2026-10-03: "For the Golden Swag key the trailer park portable cabin
    marker is not highlighted when the quest is highlighted as goal quest"): pointing at a quest, or picking it,
    lights the locks of the keys it needs on that map (its `neededKeys` there and its objectives' own keys, the
    sources BRING's "key for …" reads; `MapContent.QuestKeys`), at any zoom and with their names. A picked quest's
    doors take its pick's colour (the first pick's, where several need the key), without the picks' ring, so they
    read as part of the pick. They are a means, not a
    goal: the guide line and NEXT still lead to the quest's own places, never to a door, however near.
  - **Switches**: a power symbol (`E7E8`) with the switch's name ("Med Elevator Power Button", "Alarm Switch",
    "Fire Trap Switch"); a name the data leaves untranslated is "Switch". The Lab has 15, Labyrinth 12. **An extract
    and its switches light together** (owner, 2026-10-03, from the map audit: you have to find the switch to leave):
    an extract's `switches` in the data, and a switch that unlocks one of those (a power switch freeing a lever, up to
    four steps through the data's "activates"; not one that locks it). Pointing at the extract (its marker, its row,
    its requirement line) lights its switches at any zoom, and pointing at a switch lights the extracts it opens
    (`MapContent.Links`, added to the focus in `MainWindow.MapFocus`). But a switch the data lists on most of a
    map's extracts (more than half; the rule of the extract list's requirement line, `MapContent.OnMostExtracts`) tells
    none of them apart, so it links nothing. "Most", not "every": with one extract put right in the data, a strict
    "every" would have lit the lever for all the others (the review of 2026-10-04). Today that is all of them: the
    audit's 34 extracts are Customs' 27, which all list one lever, and The Lab's 7, which all list the Med Elevator's
    three buttons, though most of them need no switch (PvE and PvP alike, checked 2026-10-03). So no extract links
    yet, and the legend doesn't mention it; links show by themselves once tarkov.dev tells extracts apart.
  - **When they show**: on a map with artwork from 1.5 times the overview (as the shops' names; Streets has 63 locks,
    Customs 36, Reserve 34), on a sheet at any zoom; their labels from 2.5 times or when pointed at; only those on
    the floor shown (The Lab's other floors would cover the sheet with floor arrows), except what is pointed at.
    Their labels come last in the label order.
  - **Hazards**: tarkov.dev's "hazard" outlines (Labyrinth's 18 traps, 2–27 m²) as a hatched ink outline, an area
    style nothing else has. The hatch is clipped to the outline, and only its lines that cross the part in view are
    drawn, at the places they have in the whole area (`MapRenderer.HatchLines`): zoomed in, a border zone's box is
    thousands of pixels wide, and every line across all of it was drawn for each frame (the review of 2026-10-04). Labyrinth's 19th "hazard", 54 × 58 m below the central hall's floor, is left out: the data
    doesn't say what it is, and hatching it would cover the hall (trap-sized means up to 50 m²). **Minefields**
    (owner, 2026-10-03: "Yes, draw the minefields as hazard areas"): tarkov.dev's "minefield" outlines in the same
    hatched style, whatever their size, wherever the map's picture doesn't draw them itself. The artwork of Woods,
    Shoreline, Lighthouse, Streets and Terminal has a group whose id starts with "mine" (`MapArtwork.ShowsMinefields`),
    so the data's outlines would only double them there; Customs (2), Reserve (1), Interchange (7) and Ground Zero (5)
    get them from the data. **Border-sniper zones** (owner, 2026-10-03, from the map audit): tarkov.dev's "sniper"
    hazards are the kill zones where the map's border snipers shoot anyone who walks in, not sniper-Scav spawns. All
    107 are named "ScavRole/Marksman", five of their eight maps have no sniper Scavs at all, and they lie at the
    edges. They get the same hatch (so the hatch means "this area kills you": traps, minefields, border snipers),
    named "SNIPER ZONE" from 1.5 times the overview, once per group of neighbouring zones. Where the artwork draws
    them (a "danger" group named "Sniper": Customs, Ground Zero, Streets; "Danger": Interchange, whose strips the
    data's zones cover exactly; `MapArtwork.ShowsSniperZones`) they are left to it; Woods (39), Lighthouse (13),
    Shoreline (10) and Reserve (3) get them from the data. **Kept to the drawn map** (owner, 2026-10-03: "What are the
    big white rectangles at the bottom of Customs, looks odd"): over artwork a hazard shows only where the picture
    draws something (ground, water, buildings), not over the empty space around the map, where many of the data's
    outlines run on. The SVGs have no background, so `MapArtwork.Ground` is the base picture's alpha, drawn once when
    the artwork loads (2048 px on the longer side) and laid under the hatch's ink as a shader placed like the artwork,
    so a frame costs nothing more. Customs' two minefields and Reserve's minefield and sniper zones lie wholly past the
    drawn map, so nothing of them shows; Woods' and Interchange's end at the map's edge. A zone is named only where its
    centre is on the drawn map. Tiles and sheets keep hazards whole (Labyrinth's traps lie inside its render). The
    hatch is quiet so the artwork reads through it: 0.8 px lines 5 px apart at 31 % ink, the outline 1 px at 47 %.
  - **Containers** on a sheet only: faint ink dots where loot containers stand, on the floor shown (The Lab has 319,
    Labyrinth 35), so rooms and corridors show from real points where no artwork draws them. On artwork they would
    be clutter (Streets has 1,282).
  - `shturmap-cli render <map> <out.png> --focus-item <key>` draws a map as pointing at a key does.

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
  uses, not a reticle, which is the Elimination glyph. Bosses and AI squads get one solid red octagon per spawn zone:
  the red octagon means "boss or AI squad spawn" (owner, 2026-10-04, from the review: until then a red diamond with a
  dark centre, which at its size was mostly collar and centre, weaker than a quest's disc, and shared its outline
  with the violet transit diamond; the octagon is a shape nothing else uses, and all red). Every mob in the data's bosses list counts except the AI PMCs
  (`pmcUSEC`, `pmcBEAR`, which come everywhere): bosses, and since the map audit (owner, 2026-10-03) Rogues
  (Lighthouse, Icebreaker), Raiders (Reserve, The Lab), cultists (Customs, Woods, Shoreline, Night Factory, Ground Zero
  21+), AF and Black Division (Terminal, Shoreline, Icebreaker), by the data's names. Each is labelled in one format
  with Plan's line and the raid card's ("Kaban 75%"): the chance on the map, then "· N% here" when less applies to
  the place: for one with several zones, that zone's share ("Kollontay 75% · 50% here"); never their product, since
  the data doesn't say the share is conditional. A squad the data lists as several groups (Raiders, Rogues) has the
  likeliest group's chance on the map, as Plan counts a boss's, and a place says its own likeliest group's when that
  is less: The Lab's 2nd floor "Raider 60%" (groups at 60, 45 and 35 %), its basement "Raider 60% · 45% here"
  (2026-10-09, from the pre-release review: such a place listed every chance, "Raider 60%, 45%, 35%", a third
  format beside Plan's). The label is ink
  like every other label (the review of 2026-10-04: it was red, which read worse on the dark ground than any other
  label; the octagon is the danger sign), and red only while the marker is pointed at, as any label takes its
  marker's colour then. A zone split in groups says this once, on its largest group; the others are
  bare octagons that light with it. Markers whose groups have the same centroid share one ("Reshala 75% · 33% here /
  Knight 25%" on Customs' Stronghold), and pointing at one lights all its zones. The Lab gets 8, Terminal 16; Ground
  Zero 21+ gets 9, mostly a 2 % cultist that may spawn at any Scav spawn.
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
- **Distances on the map.** The guide line to the nearest pick carries the card's number on a small dark plate with
  a hairline in that pick's colour at the middle of its part in view ("69 m", the same horizontal distance and rounding as the
  card, `MapRenderer.DistanceText`). **The distance and nothing else** (owner, 2026-10-04: "The 'minutes' numbers
  when showing distances on the map are completely off. Either be precise or scrap them at all"): until then the
  plate added the position's age once it was a minute old ("69 m · 4 MIN"), and minutes beside a distance read as
  the time it takes to get there, which Shturmap can't know. The age is said at the player's marker, always with
  "OLD". A line too short to carry it clear of its ends has none. Where the middle would cover a
  symbol, the plate slides along the line to the nearest place that covers none, toward the place first, and keeps
  the middle only when the line has no free place (the review of 2026-10-04: it stood on a boss marker). A scale bar
  at the lower left,
  above the wiki link and the credit line, takes the longest round length (1, 2, 5, 10, 25, 50, 100, 200, 500 m …)
  that fits in 120 px and follows the zoom. Its metres are those along the bar, the screen's horizontal (the review of
  2026-10-04: it took the mean of both axes, and on Icebreaker, whose render is stretched 1.75× along one axis, "50 m"
  spanned 69 m; there a metre up or down the screen is 1.75 times a metre across it, so the bar holds across only).
  No range rings (three circles on every view for a question the bar and
  the plate answer) and no north arrow (the artwork's orientation isn't verified; the cards give directions
  relative to the facing).
- **How far the view zooms.** Out to half the zoom that shows the whole map, in to 64 px a map unit. The limit is
  the drawn map's, set again with every frame (`Camera.LimitTo`): until the review of 2026-10-04 it was the map's that
  was fitted last, so after a preview of another map the first turn of the wheel jumped to that map's limit. A view
  outside the limits moves toward them or stays, never jumps. A map fitted while the view has no room (the window
  minimised, or not laid out yet) is fitted when it has; before, its zoom fell to almost nothing and the map stayed out
  of sight until "show the whole map".
- **Room for labels on the right** (owner, 2026-10-09). The map writes a symbol's label to its right, so the whole map
  (a new map, a Plan row's preview, 0 and the button) and every framing (What's New's and the tour's previews, the raid
  replay) keep 150 DIP between what they show and the view's right edge (`Camera.LabelRoom`); the other sides keep
  their padding (24 DIP for the whole map, 40 for a preview or replay, 90 for the tour). Until then the right side had
  only the padding, and on a wide map the rightmost names ran off the window (Customs: "Transit to I…", "Crossr…"; the
  tour's picked quest past its 90 DIP). The room is at most a fifth of the view's width (`Camera.LabelRoomShare`): at
  the smallest window, the map about 500 DIP wide, 150 would shrink a wide map by more than a quarter; 100 shrink it by
  a sixth, and a long name there may still lose its last letters. What fits with room to spare stays in the middle (a
  tall map such as Streets in a wide view); otherwise it moves left only as far as the room needs
  (`Camera.FramingAbove`, the one place a view is worked out). "The zoom that shows the whole map" (the zoom limit, when
  the map's smaller names show) is this view's.
- **The player at full strength.** The sand disc no longer fades with age (it sank to 45 % after 3.5 minutes,
  below the quest markers; transparency reads as "less important", the reason half-strength other-floor markers
  were rejected). It has the picks' vocabulary instead: a steady ring, sand on a dark band, at 12 px. Once
  the position is a minute old the ring turns dashed and a small dark tag beside it gives the age in whole units,
  as the top bar does, with "OLD" ("4 MIN OLD", "2 H OLD"). The facing cone has its arrow outside the ring, and shows for the 45 s the
  cards' directions are relative to the facing (principle 8, `Facing.Fresh`; until the review of 2026-10-04 for 60 s).
- **One meaning per colour, shape as a second cue.** Simulated colour blindness (Machado 2009) put the quest amber,
  the PMC-extract green and the shared-extract khaki within ΔE 4–7 of each other. Shared extracts are split down
  the middle by a dark line (two sides, one exit), not hollow. Done
  objectives are a smaller muted-ink disc with a check mark, no longer translucent green, so green means extracts
  only; their zones turn muted too. The trail is the player's sand, no longer the Scav-extract teal. Sand is the
  player's alone (the review of 2026-10-04: other-floor badges and the squares of an item's loose spots were sand
  too): a marker's floor badge wears the marker's colour, as its count and "OPT" badges do, and a loose spot's open
  square is ink, like the padlocks a key pointed at lights and the container dots: what the map's data has, neither
  a quest's nor the player's.
- **Clusters of one objective's places.** Places of one objective (and one kind) whose markers would overlap,
  closer than two marker widths on screen, merge into one marker at the group's medoid (a real place, so the
  marker never stands where nothing is) with a count badge at its lower right in the marker's colour: "Following the
  Bread Crumbs" lists 16 places on The Lab, which piled up with four labels. Groups split as the view zooms in;
  places of different objectives never merge; possible places keep their "?"; the kept or pointed-at quest clusters
  too. **Possible places** (one of several places a thing can be) are drawn like every quest marker, the type's
  dark glyph on the marker's colour, with a "?" at the lower left, in the badges' look (owner, 2026-10-04: "The
  'Mark' icon is the pin needle, which is black on background. The 'Get' icon is a hand that is color on
  transparent background. I think it should always be a black icon surrounded by the marker color"). Until then a
  possible place was a hollow ring with the glyph in colour, meant to read as "maybe here"; it read as another kind
  of marker, its glyph taken for the difference. The four corners are then: floor upper right, count lower right,
  "OPT" upper left, "?" lower left. **Only where there are several**: tarkov.dev lists every quest item's spot as a
  possible location, also where it is the only one (57 of 109 such objectives in PvE, 2026-10-06: Population Census's
  journal, Audit's records, Golden Swag's lighter), and those were drawn with a "?" that said nothing true (owner,
  2026-10-06: "Why does the Population Census have a question mark in the icon?"). A thing with one place is drawn at
  it as a place like any other (`MapContentBuilder.PlacesItCanBe`); with more, across every map, each is a possible
  place, and the quest's card says so ("Quest cards"). A group with any place on another floor shows the floor arrow. The largest group carries the label;
  pointing at a cluster points at its quest. Only places in view are merged, so a group that runs out of view keeps
  a marker for the ones in view, with their count (the review of 2026-10-04: merged first, the whole group went when
  its middle place was out of view).
- **Objectives of one quest on one spot: one marker that says how many** (owner, 2026-10-06: "Sometimes you have two
  objectives to do on exactly the same spot. For example, for Gratitude on Woods you have to stash Shemagh and Ray
  Bench glasses on exactly the same spot and it shows as two icons marking the same spot … we should join the two and
  annotate the icon properly"; chosen from a panel of five ways drawn by the real map on four spots: "D, count, but
  make it look nice"; `MapRenderer.JoinSpots`, `MapJoin.cs`). Places of different objectives of one quest within
  1.5 m across and up are one spot (in PvE 65 pairs on 2026-10-06: Gratitude's two stashes, Hobby Club's four,
  Bullshit's three, a "locate" and its stash or mark, as in Fishing Gear, Drug Trafficking, Humanitarian Supplies). The
  first open objective's disc stands for them, and **a tab** joined to its right says how many are open there: "×2". Disc
  and tab are one shape, gold with the marker's dark outline and collar, the tab's far end rounded as the disc is, and
  a soft dark hairline where the tab leaves the disc so the disc still reads as one; in the tab "×" a little smaller than
  its figure, both dark as the glyph. A tab, not the corner count: that one is a cluster's, places of one objective
  that part as the view zooms in, where these stay one spot. The name stands after the tab; the floor arrow moves
  above it; the tab is the marker for the pointer, and pointing at any of its objectives (a row on the card, a line in
  the raid card) points at the spot. A ticked objective leaves the count: with one left open the marker is a plain disc,
  with none a done one. "Maybe here" places don't join: a "?" couldn't say which objective is only maybe there.
  Also drawn and rejected (the panel, `shturmap-cli join-panel`, removed with the choice): each objective on its own
  disc set apart on leaders to the one spot (as before); a pile of discs; the glyphs side by side in one wider shape;
  the pile with the things' names in the label.
- **Places out of view.** While quests are picked, or one is pointed at, their places outside the view are shown
  as small chevrons 18 px in from the edge, toward them from the middle of the view, one per direction (places
  whose edge points lie within 56 px merge) with how many lie that way, in the quest's colour (its pick's colour
  when picked, gold when pointed at). Done objectives don't count. Same vocabulary as the player's edge badge, smaller and
  without a plate: the player is level 1. Nothing is drawn when nothing is highlighted.
- **The raid replay** (owner, 2026-10-07: "a post-raid view, similar to the one in Arc Raiders ... only the positions
  from the screenshots ... more on the eye-candy side of things ... automatically played after the raid finishes"; from
  the panels "B and D", then "C, but encode raid time to the color of the pen stroke. Use a fitting color map based on
  the apps design", "Replay timeline 1", "Replay link: ii"; §1's exception). What there is to replay (a week of the
  study log, about twenty raids): one position per four or five raid minutes, a third of the raids with one, the
  median four, gaps of 10 to 20 minutes in long raids, the last position up to 20 minutes before the end line; how a
  raid ended is in no log Shturmap reads. So the replay shows where and when the player took screenshots, and when
  they were last seen, never how the raid ended.
  - *What is kept* (`GameSession`): each position on the raid's own map with its minute since the raid's start
    (`WallClock`), the objectives ticked during the raid with their minutes, and when a screenshot first showed the
    extract list. A raid loading starts them anew, so after a transit only the map the raid ended on is replayed. At the
    raid's end, when the log has its end (its length is known), they make a `RaidReplay`, which comes with the RAID
    OVER cue and stays in the snapshot until the next raid loads. In memory only: never on disk, never in the app log
    (§2), gone at a restart.
  - *When it plays*: at RAID OVER, when the raid has 3 positions or more over 5 minutes or more (`RaidReplay.Plays`;
    about half of that week's raids); otherwise today's cue. Never for the log read back at start. REPLAY on Plan's
    last-raid line plays it again (the raid's map comes back on screen first) until the next raid loads.
  - *The sequence* (`ReplayTiming`, `MainWindow.Replay`): RAID OVER enters in the middle as every cue does (1.6 s), then
    fades as the band slides up to the map's foot and the view glides to the raid's positions (1 s); the raid plays in
    11 s whatever its length, holds 3 s on its end, and fades (0.6 s): about 17 s, no clicks (owner, 2026-10-08: "can
    easily be a bit longer"; until then 7.5 s of play and a 2 s hold, about 12.7 s). The band stands 46 DIP
    above the map's foot, so the WIKI MAP link and the artwork's credit stay readable, and right of the map's buttons.
    A new cue, a raid loading or a preview ends it. With Windows' animation effects off, and in snapshots, the end shows
    at once for the time it would have played (a snapshot keeps it).
  - *The view*: framed on the positions with 40 DIP around them (on the right the label room; "Map drawing", *Room for
    labels on the right*) and at least 300 m across, in the part above the band (`MapView.FramingAbove`). It stays
    there afterwards. A replay that ends while the view still glides there (a raid loading, a preview) stops the glide,
    and so does a new map in the view (review of 2026-10-09: the glide went on moving the next map). The map recedes
    under the replay (the ground at 45 % over it),
    and everything of the replay fades in and out as one (`MapScene.ReplayOpacity`).
  - *The pen* (C; `MapRenderer.DrawReplay`, `ReplayInk`): one line from position to position, drawn on as the time
    runs, growing toward the next position. Its colour is the raid's time: the player's sand mixed into the ground,
    opaque, from 22 % sand at the raid's start to all of it at its end, and it widens from 1.4 to 3.2 DIP, over a dark
    collar (the ground at 59 %, 6.2 DIP). One hue because sand is the player's alone: any map of several hues
    (viridis, magma, cividis) runs through the map's taken colours, green and teal for extracts, violet for transits,
    amber for quests, red for bosses. Light means late, as the newest position is the brightest thing on the map, and
    width is the second cue ("One meaning per colour, shape as a second cue"). Tried on a raid from the study log
    and dropped: sand by alpha (the artwork showed through, and the overlapping pieces dotted the line), a range from
    35 % sand (too little difference). Each position is a dot (2.6 DIP) in its time's colour on a dark collar, with its
    minute beside it, "8 MIN IN" (never bare minutes, which read as a time to get somewhere, owner, 2026-10-04; the
    panel's "8 MIN" changed for that), ink on a dark tag set where it covers no other tag or dot and stays above the
    band, else left out. The newest position pings as a new position does. At the end the last one's tag says "LAST
    SEEN · 2 MIN BEFORE THE END" ("JUST BEFORE THE END" within a minute), framed in sand as an old position's tag is.
    Positions on another floor are drawn as on the floor shown. A straight line between positions minutes apart reads
    as a route Shturmap doesn't know; the owner chose the pen knowing that, and the minutes and the timeline show the gaps.
  - *The timeline* (1; `Controls.ReplayBand`): the band's words, RAID OVER in amber and CUSTOMS · 33 MIN in ink in the
    cue's eyebrow, and "6 OF 9 POSITIONS" at the right; the raid's length as a 4 px rule with a tick every ten minutes
    and at its end, its played part in the pen's colours along the whole rule (so the rule is the colour key) and the
    rest `LineStrong`, the playhead (2 × 14 DIP, ink). Above the rule a dot per position, in its colour once played and
    an outline before; below it a check (`E73E`, the done objective's) per objective ticked during the raid and a green
    triangle (the PMC extract's shape and colour) where the extract list was read; "33 MIN" at the end.
  - The legend has a row for it, listed while it plays on the map.
- **A quest completed: its places ring out** (owner, 2026-10-07, with the QUEST COMPLETE cue, §4 principle 11). The
  places of a quest the log just reported completed don't simply vanish: each turns into a gold disc with the done
  objective's check that pops once, two gold rings leave it as a ping's do, and it fades, 1.6 s in all
  (`MapScene.Leaving`, `MapRenderer.DrawLeaving`). It helps the eye see which places went. The places are those the
  snapshot without the quest takes off the shown map, matched with the cue whichever comes first
  (`MainWindow.Completion`). Nothing with animation effects off: the places go. The legend has a row for it wherever
  a quest has a place.
- **Legend.** The help panel's ON THE MAP rows are drawn by `MapRenderer` itself (`MapLegend`: one small bitmap per
  symbol, made with the map's own drawing code at twice the DIP size), so they can't drift from the map; the hand-drawn
  XAML shapes showed a plain disc for the quest marker, which has a collar and a glyph. Rows go by the four levels:
  first what levels 1 and 2 always draw (you, the picks, objectives, extracts and transits), then what marks those or
  shows only at times, then levels 3 and 4 (owner, 2026-10-09: the extracts came after the ping and "Moved off a crowded
  spot", so help's twelve on Streets left them behind the link). Every symbol on the map has one (the done objective,
  quest zones, the floor arrow, the guide line and its plate, the trail, the edge badge and chevrons, clusters and the
  sheet were missing; and until the review of 2026-10-04 the gold chevrons of a pointed-at quest, the padlock in a
  pick's colour of a door it needs a key for, and the ping of a new position), and each names shape and colour, not
  colour alone. A new symbol gets a `LegendSymbol` and a row in the same change, and a line in `MapLegend.On`. Help
  lists the symbols the map on screen has under ON THIS MAP, the twelve most important of them, and keeps the rest of
  them and the symbols the map doesn't have (under NOT ON THIS MAP) behind one link, "SHOW n MORE SYMBOLS" (owner,
  2026-10-04: every symbol of every map made help 2,350 px tall; 2026-10-09, help trimmed: Streets alone listed 26;
  `LegendFold`).
  A symbol counts when the scene holds what it stands for (`MapLegend.On`): a padlock when the map has locks, the
  guide line when a pick has a place here and there is a position, the sheet when there is no artwork. What only
  shows for a moment counts where its cause can occur: the ping wherever there is a position, a pick's chevrons and
  padlock where a pick has a place or a door here, the gold chevrons wherever a quest has a place (any of
  them can be pointed at). The list
  follows the map while help is open; with no map up, all rows count as ON THE MAP (twelve, then the link).

### Quest cards

One card per quest, the same everywhere: trader portrait, type glyph and name; trader, level and Kappa /
Lightkeeper; the state and where it came from ("Active · from the game log, 25 Sep"); every objective with its
glyph, where it is, the item it is about and, in a raid on its map, how far and which way it is from the last fix
("69 m · AHEAD-LEFT · 8 M UP": the raid card's style, the figure as it is and the direction and height in capitals;
until 2026-10-09 in small letters, beside the raid card's capitals); BRING (keys and items, with icons and maps);
UNLOCKS (the quests it opens); a link to the wiki page. Nothing else: no rewards, no guides.
The state line is in the accent while the quest is active ("on") and muted for every other state. It was green,
which on the map and in the rail is a PMC extract (one colour, one meaning; the review of 2026-10-04, B2).
An objective says where it is once (the review, C2): the line of map names under its text is left out when the
text itself names the map as its place ("… on Streets of Tarkov" stood over "Streets of Tarkov"), alone or in a list
that holds every map the line would show. A text that names none of its maps, or only some, keeps the whole line;
"at Factory gate" is a gate, not the map (`QuestCards.SaysWhere`, with the synopsis' map pattern; English texts
only, so in another game language both lines stay).
**The map's "?" in words** (owner, 2026-10-06: "It should be displayed in the raid tooltip to explain"): an objective
whose thing can be at several places has a line under its map names, the "?" as the map draws it (a small dark plate
with a gold hairline) and "One of 4 places it can be", on every card, held or on hover in a raid
(`CardObjective.Possible`); its distance is to the nearest of them. A done one has no such line.

**Ticks: an objective the player says is done** (owner, 2026-10-04; UX principle 7). The game's logs never say that
a single objective is done, so a quest that takes several raids kept leading to places already dealt with.
- **Where.** Each objective's row on an active quest's card (hover or held) ends in a small square box, the
  look of the ticks in settings (hairline; amber with a check when ticked). One click ticks, another unticks. Its
  tooltip says what it does and that the logs don't say it. Nothing is asked, and the rail has no tick: it stays
  calm, and the card is one point away. A tick can be set any time, also in a raid, but nothing ever needs it.
- **Says why.** A ticked objective is muted, as done things are on the map, and where its distance stood it says
  "Done · ticked by you, 4 Oct" (the day of the tick). The quest's state line is untouched: it is the log's.
- **Kept.** Per game mode, in the settings beside the picks (`ticks.<mode>`, each with its day; `ObjectiveTicks`).
  A tick leaves by itself when the log reports its quest completed or failed; one whose objective the data doesn't
  know stays. Only an active quest's objective can be ticked; a tick can always be taken back.
- **What follows**, all from the one set of ticked objectives: the map draws the objective's markers and zones as
  done (the small muted disc with a check, at its rest size and without a name, also while the quest is picked or
  pointed at); its places leave the guide line, the chevrons and NEXT; the raid card mutes its line ("Screen
  anatomy"); the planner sees the quest without it (§7), so COMPLETE and PROGRESS, the summary, the synopsis, the
  progress note, the need cells, BRING, the kit and the effort order all speak of what is left; the quest card's
  BRING and the item card's uses list only what open objectives need; a Scav raid's FIND IN RAID drops what a ticked
  objective asked for. A quest with no raid work left appears on no map. Its type glyph stays the whole quest's.
- Developer builds: the study log records `tick` and `untick` (objective, quest, how: card, done, dev); the
  developer script's `tick <quest> <n>` sets one for the session only, and `show <quest>` holds a card for a snapshot.

**Hand-overs: a mark on what gets the thing, not a line** (owner, 2026-10-05: "many quests have a thing where you
have to get something or pick up something and then at a later step you need to hand it off after the raid. That
last step is out of raid but still takes up a 'ToDo' line in the quest overview. I feel this should rather be an
icon associated with the corresponding item or quest item. It should not be AI derived per quest, it should be
detectable from the quest text or layout itself").
- **Which.** A hand-over folds into the objective of the same quest that gets the same thing (`Handovers`), by the
  data's ids, so it holds in every game language: `giveQuestItem` with the `findQuestItem` of the same quest item;
  `giveItem` with the `findItem` of the same set of items (any order), the same count and the same found-in-raid.
  Each pairs once, in the quest's order (two quests list the hand-over first). Checked on 2026-10-05: in PvE 217 of
  391 hand-overs fold (95 of 99 quest items); in PvP 217 of 403. What doesn't fold is the quest's own work at the
  trader and stays a line, as before: money, "any found in raid medicine items", figurines, and a quest item got in
  an earlier quest (Postman Pat - Part 2's letter, Kind of Sabotage's folder, A Healthy Alternative's journal,
  Shipping Delay - Part 1's package). Words weren't used: the hand-over's text is often just "Hand over the items".
- **Optional.** Where the find is optional and its hand-over isn't (the game counts the finding as optional where
  the items may be bought or found anywhere: A Bitter Victory, Reserve Expert, The Huntsman Path - Eraser), the
  line reads as required, without "(optional)" or OPT (`Handovers.Optional`): what is handed over has to be got. The
  planner still goes by the data's flag.
- **The mark: the item, to the trader** (`Controls.HandoverMark`; owner, 2026-10-06: "Right now this icon uses up an
  entire row in each quest. It should be a proper symbol, but maybe attached to the left or the right of the icon of
  the item"; chosen from a panel of five ways drawn at the app's sizes from the real data: "Hand-over: E"). Right
  after the cell of what the objective gets: its count when more than one ("×4", as after a need cell; a find's text
  from the data doesn't say it), a small solid arrow in muted (Phosphor's "arrow-right") and the portrait of the
  trader it goes to, the quest's: "battery ×4 → Therapist". No symbol to learn, and the portrait says to whom. On a
  quest card (hover or held) it follows the objective's 30 px cell, with a 22 px portrait; in the raid
  card the line gets the item's 20 px cell with the mark, a 20 px portrait, between its words and its distance (the
  cell is that item, as a need cell is). The tooltip says it in words: "Hand over ×3 to Therapist after the raid".
  NEXT has none (a glance is for where to go). A ticked line keeps its mark: what was got still goes to the trader.
  Before it: the handshake alone, 12 px and muted, in the corner of the item's cell, "barely visible and you cannot
  tell what that actually is nor what it means" (owner, 2026-10-05); then a tag with the handshake and the words
  ("HAND OVER ×3 TO THERAPIST") under the objective's lines, which took a line on every hand-over. Also drawn and
  rejected (the panel, `shturmap-cli handover-panel`, removed with the choice): a tab on the cell's right or left
  with a hand passing something up (Phosphor's "hand-arrow-up", bold), and the tab with a forward arrow; a new
  symbol to learn, where the portrait needs none.
- **What follows.** The quest card and the raid card lose the hand-over's line; a quest whose lines on this map are
  all ticked leaves the raid card (the hand-over line used to keep it there, "after the raid", with nothing to do in
  the raid). The item card says it once, "Pick up, then hand over · Ground Zero", "Find in raid ×3, then hand over";
  once the find is ticked, the hand-over is what is left and says so itself ("Hand over ×3, found in raid"). Plan
  never listed hand-overs (it counts in-raid work only), and FIND IN RAID already counted a find and its hand-over
  once.
- `shturmap-cli handovers [mode]` lists every fold and flags NEAR (a hand-over sharing items with a find or pickup
  of its quest that doesn't fold, and why) and quest items handed over with no pickup in their quest; run it after
  a tarkov.dev update ("Updates and checks", docs/UPDATES.md).

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

**One row per thing, counted as Plan counts it.** A quest card's BRING and the item card's lines are gathered over
the quest's objectives the way Plan's BRING is (`RaidPlanner`'s requirements): what is used up adds up (three "mark"
objectives are "MS2000 Marker ×3" and "Bring ×3, to mark"), one item used two ways is one row saying both ("to plant
and to use"), and a key or item needed on two maps names both. Gear to wear, weapons and mods aren't used up and stay
rows of their own; an exit's items are counted once per exit. Until 2026-10-04 the card made one row per objective and
dropped a row whose text was already there, which lost the count and the second map.

**A quest item is never brought** (owner, 2026-10-06: "In mission one less loose end, the lab journal is marked as a item
to bring, but it is one you actually have to collect first as part of the mission"). A quest item an objective stashes
(`plantQuestItem`) is got by a "locate and obtain" of the same quest (14 of the 15 such objectives on 2026-10-06) or of
one before (Hobby Club's handguard, from Fair Price - Part 2). On pickup it goes to the quest items, and from there into
every raid by itself (the game asks "You have quest items in your inventory! Proceed?"; it is lost only with a death, or
left behind when parked in the quest items stash): there is nothing to pack. So it is in no BRING row, need cell or kit,
and the item card says "Stash · Woods", not "Bring, to plant". The raid card's line says where it comes from instead,
in the place of a "Bring:" line: "With: Lab journal, found on Factory", "With: AK-50 handguard, from Fair Price - Part 2"
(`Planning.QuestItemFrom`). Until then each stood in BRING as an item to take along.

**An offer behind a quest isn't a way yet** (the review of 2026-10-04: BRING named "Ragman LL2 · 41,283 ₽ · after
Dandies" as the easiest source of the beanie Dandies itself asks for). A trader's offer or a barter that tarkov.dev
ties to a quest (`taskUnlock`) which the game's log hasn't seen completed comes after every other way (other
offers, barters, crafts, the flea market, loose spots) and says "after <quest>"; so the one line under a BRING row
names it only when nothing else is known. Once the log has seen that quest completed, the offer is one like any
other, in its place by price, and the note goes. Of several items that will each do (a weapon class), the line names
the first with a way open now. Shturmap knows no loyalty level, so "LL2" stays a fact about the offer, never a claim
that the player has it (`ItemCards.Sources`).

**What kills and exits take** (owner, 2026-10-03: "There are others that still require items, such as doing kills
with certain weapons or weapon classes … It should be a coherent design so it is still clear what to bring and what
is needed to solve a quest"; and "in the cease fire quest it is not clear that we need to bring a flare"). BRING
keeps one row shape (icon, what, what for and for which quests, where to get it) and adds the verbs:
- **"to use"** for the weapons a kill objective names (`usingWeapon`, any one will do). tarkov.dev arrives with a
  class as every member, so the weapons are grouped by their item category (tarkov.dev's `categories`, most
  specific first; presets, which repeat the weapon they build, don't count) and said in at most three parts: a
  group that is at least three quarters of its category, or more than ten weapons, by the category ("Any sniper
  rifle", "Shotgun (13 of 16 kinds)", "Assault rifle (15 of 53 kinds)"); if that takes more parts and the weapons
  span several categories, every group of two or more by its category ("Any handgun or revolver (3 of 5 kinds)");
  otherwise, and for a few weapons of one category, the weapons ("Colt M4A1 or 5 others"), so there is a gun to
  name. "Any" only for the whole category; a count says how many kinds will do. Whole categories come first, then
  parts of one, then single weapons ("Any sniper rifle or MP-18 7.62x54R single-shot rifle"). The categories load
  with the item sources, after the rest; until then the list form. A class row's source line names one of them ("e.g.
  Mosin rifle · Prapor LL1 · …").
- **"to fit"** for the mods the weapon must carry (`usingWeaponMods`), shown like gear ("Valday PS-320 1/6x scope /
  AK-12 5.45x39 sound suppressor").
- **"to leave through <exit>"** for what the exit an extract objective names takes: the objective names it by the
  game's internal name (`exitName` before translation, `ObjectiveFacts.Exit`), the same as the map's extract, and
  `ExtractRules.Items` gives the items by the rules the extract list already uses: a red signal flare at a flare exit
  (Cease Fire!, Belka and Strelka), ice pick and paracord at a climbing exit (Payback), the roubles or item a paid
  exit asks for ("Roubles ×5,000: to leave through Primorsky Ave Taxi V-Ex"). Transits stay with their own text.
- **"to enter <map>"** for what the map itself takes to deploy (tarkov.dev's `accessKeys`: The Lab's access keycard,
  Labyrinth's, Icebreaker's marine repair kit; owner, 2026-10-03, from the map audit). It is the first row of the Plan
  card's BRING and the raid card's, the first of the map card's need cells, and shows with no quest picked or none
  there, since every raid there needs it (a Plan that ranked The Lab first never said it takes a keycard).
- **Gear a kill forbids** (`notWearing`) is nothing to bring, so no row: a note on the objective's line in the raid
  card, "Without: armor, headwear" (the item categories it spans; before they load, the items).
- One row per weapon set, mod set or exit item, listing every quest it serves; the quest rows' need cells, the raid
  card's objective lines ("Use: …", "Fit: …"), the quest card's BRING and the item card ("Use, for kills", "Fit, for
  kills", "Bring, to leave through …") say the same. Only what the data says: Shturmap knows no inventory, so never
  "you have it". `shturmap-cli bring [mode]` lists every map's rows and flags an item without a name, a weapon list
  shown as "X or N others" (with the categories it spans) and an exit no map has; run it after a tarkov.dev or game
  update. Checked 2026-10-03 (PvE and PvP): no unknown items, no unmatched exits.

Cards behave like the nested tooltips in Crusader Kings III:

1. **Hover**: rest the pointer on a quest, key or item for 0.65 s on a rail row or map marker (the study log: 62 %
   of cards opened from lists closed within a second, opened by a pointer passing over), 0.4 s on a row on a card,
   and its card appears beside it, see-through (80 %) so it doesn't hide the map. It stays while the pointer is on its
   subject or on the card, and goes 0.35 s after it leaves both. Moving down a list swaps it after 0.12 s.
   **On its way to the card the pointer is only passing over** (`CardAim`; owner, 2026-10-04: "When you then move
   the mouse to the right to mouse-over the quest and you are too slow another quest opens"). A card opens beside
   its row, so the way to it leads across things with cards of their own: the other glyphs of a map's row, a need
   cell, the rows below. While the pointer keeps heading for the open card (it moved at least 3 px, came nearer,
   and its line carried on meets the card, with 16 px of slack), what would replace the card waits and what would
   close it waits, each looked at again every 0.3 s; a pointer that rests on the other thing, or turns away, gets
   that thing's card. It is the direction that counts, not the speed (the "safe triangle" of nested menus). A held
   card is spared the same way. Checked in the app by script on 2026-10-04: onto another quest while heading for
   the card, the card was still there 0.2 s later and the other quest's 0.6 s after that; not heading, it had
   swapped within 0.2 s; left into a gap and still heading, it was there after 0.45 s, where a resting pointer
   had lost it.
2. **Held**: a click holds it: on the quest (row or marker), or anywhere on the card. A held card is solid with an
   amber border and stays while the pointer is near it. It closes on a click on nothing in particular (bare rail
   or map), Esc, another click on its quest, a full rest on something else that opens a card in its place (0.65 s
   from the rail or the map, 0.4 s on a card), or the pointer moving more than 240 px away from both the card and
   what it was opened from, its row or its marker (`CardReach`; 2026-10-04: measured from the card alone, a click on
   the left of a row held a card that closed with the next move of the mouse). Nothing holds by itself (owner,
   2026-10-01: the timed hold was dropped). And every card closes at a step into a raid, when
   it begins to load and when it starts (`WhileInRaid`; review of 2026-10-04: a card held in Plan stayed over the
   map through the loading and the whole raid, with nobody at the mouse to click it away). A card opened during
   the raid stays as any held card does.
3. **Nested**: on a card, rest on a key, an item or a quest (UNLOCKS, NEEDED FOR) and that one's card opens beside
   it, and so on; a click holds it too. Moving back to an earlier card closes the unheld later ones.

Popped-out cards were removed on 2026-10-09 (owner): picks put the chosen quests first in the raid card, with live
distances, in the window already on the second monitor. The `pinned.cards` row an older database holds is no longer
read.

A card is never taller than the room there is in the window. A longer card (Collector, a quest with many
objectives at 150 % scaling) scrolls inside, so UNLOCKS and the wiki link stay in reach (2026-10-04: it was cut at
the window's edge).

Rows on a card take part in linked highlighting (pointing at a key lights it up in BRING and the quest on the map),
but don't light up for their own card's quest, or the whole card would glow. The card's body keeps its subject in
focus all the same, without a tint: the quest on a quest's card, the item and its quests on an item's card
("Visual language", linked highlight; 2026-10-04). A BRING row that stands for several items is each of them. An objective's row is its objective:
pointing at it marks that objective within the quest (on the map only its places pulse; in the raid card its line
takes the tint), and the row lights up when one of its places on the map, or its line in the raid card, is pointed
at (the review of 2026-10-04). Rows that show the same item still light together, as before.

### Picks: the quests for the coming raid

Pointing highlights for as long as the pointer stays; the quest's pen (on its rail rows and its card) **picks** it
for the coming raid (owner, 2026-10-03: "you typically bring items for specific quest or want to run a specific
quest. Therefore you likely want to have something like the current 'mark quest' thing, but for all the quest you
want to tackle in the map. Still it should show all other quest markers."). It grew out of the single "kept"
quest (owner, 2026-10-01: keep a quest's markers easy to find while you look away): the same pen, the same look,
now for as many quests as you like, and the pen keeps its one meaning, "keep lit". A click on the quest itself
still only keeps its card open (two clicks, two meanings).

- **One click per quest while planning, none in the raid.** Picks hold from Plan through loading into the raid
  and across restarts (the settings, `picks.<mode>`; PvE and PvP keep their own, `QuestPicks`).
- **Picks belong to a map** (owner, 2026-10-04: "Store the selected quests per map and persistent between
  sessions"). A pick is "this quest, on this map": what is picked for Customs waits there while Streets is planned
  with picks of its own, and comes back when Customs is shown or loads. Until then picks were one set for all
  maps: a quest with work on five maps was picked on all five and brought all five to the top of Plan, and the
  colours of one map's picks were used up by another's. Now:
  - *The pen picks for the map the rail's quests are of*: the map whose card is open in Plan (the map shown; the
    best suggested one when the map shown isn't among them), in a raid the raid's map. One click as before; there
    is no map to choose. A quest with work on several maps is picked where the player picked it, and its pen is
    lit only there.
  - *Each map shows its own*: its picks first in its card and in its row of Plan's list, the map draws the picks
    of the map drawn, and a map counts for Plan's order only the picks made on it.
  - *Each map hands out the colours from the first* ("A colour per pick", below), so a map's two or three picks
    always get the four colours that are easiest to tell apart, however many quests are picked elsewhere.
  - *CLEAR PICKS clears the map shown*; other maps keep theirs.
  - *A map and its variants share their picks* (Ground Zero and Ground Zero 21+, Factory by day and by night:
    `Planning.PickKey`, the map as the player picks it in the game).
  - *Kept as one setting per mode*, `picks.<mode>`: "map|quest:colour" entries. Picks saved before 2026-10-04 had
    no map: each becomes a pick on every map its quest has work on, as it was shown until then, once the data is
    loaded (`QuestPicks.Adopt`); one whose quest is no longer active is let go.
- **No upkeep.** A pick stays until the log reports the quest completed or failed (it leaves every map's picks by
  itself), its pen is clicked again, or CLEAR PICKS (beside NEXT RAID, outside raids). Picks don't clear at raid end: a quest often
  takes several raids, and a plan that empties itself would have to be made again each time. Esc never touches
  picks (it closes the cards): a key that throws a plan away would be too easy to hit. Only an active quest can be
  picked; a quest whose state the log doesn't tell stays picked.
- **The look.** Picks have colours of their own, one per pick ("A colour per pick", below): owner, 2026-10-01, gold
  among gold didn't stand out. Until 2026-10-04 that was one cyan for every pick (`Kept`, now the dev build's icon
  only), chosen as the one hue nothing else on the map uses, the artwork included, and apart from gold with any
  colour vision. Their rows, and the BRING rows that serve them, keep a tint of the pick's colour; on the map their
  markers take it, at their rest size, inside a steady ring of it on a dark band that lies under every symbol
  (until 2026-10-05 they grew to 14 px, and what was pointed at to 12, over neighbours set apart for 10; "Map
  drawing", *Symbols that cover each other*), their zones take it, and a dashed line runs from your last fix to the
  nearest place of any pick, in that pick's colour, with the distance on its plate (the raid card's NEXT names the
  same objective). Their places
  out of view get chevrons. Picks hold still: a marker pulsing all raid would be motion at the edge of the
  player's eye; what the pointer is on pulses.
- **A colour per pick** (owner, 2026-10-04: with several picks "it is then, however, difficult to distinguish on a
  very quick look which items belong together"; from a panel of five ways drawn by the real map, "the A color per
  pick ... is the best solution. Make sure the 'opt' icons attached to it and others change colors as well"). The
  colours are eight soft ones, the first eight of ColorBrewer's "Set3", a table made for telling categories apart
  on maps: aqua, salmon, blue, lime, pale yellow, pink, lavender, orange (`Pick1` to `Pick8`;
  `MapRenderer.PickColors`); a ninth pick takes the first again. The ring and the larger size say "picked" without
  colour. **How they were chosen** (owner, 2026-10-04: "When selecting many quests the system chooses currently
  very similar colors ... Maybe even different shades of pastel colors helps and hand-selecting a few. Make a panel
  of the best options possible"; then, from that panel, "Go for D"). The first version had four strong colours
  (cyan, pink, lime, orange) that repeated from the fifth pick. Six palettes were drawn by the real map with ten
  picks and measured with CIEDE2000, normally, as seen with red-green colour blindness, and against the map's own
  colours: the four strong ones were 39 apart at their closest but 6 for colour-blind eyes; five colours can be
  kept 25 apart (16 colour-blind) and were suggested with a second ring from the sixth pick on; this table's eight
  are 14 apart at their closest (pale yellow and lime), 2 for colour-blind eyes (orange and lime), and its orange
  is 13 from the quests' gold. The owner chose the soft table: calm on the map, and eight different colours. The
  order picks take them puts the four easiest to tell first (aqua, salmon, blue, lime) and the orange last, so two
  to four picks, the usual number, get the best of it. Everything of a pick has its colour: marker, ring, the badges at its
  corners (floor, count, "OPT", "?"), its name, its zone, its chevron at the edge, the padlock of a door it needs a
  key for (the first pick's, where several need it), the guide line and plate when they lead to it; and in the
  rail the tint of its row and of the BRING rows that serve it, its pen, its glyph in a map's row, and the frame
  of its items in the loading cue. A pick keeps its colour until it is unpicked, across restarts
  (`QuestPicks.Slots`, kept with the pick): unpicking one quest recolours none of the others on its map, and the
  next pick takes the colour that came free. The other ways on the panel: a number per pick (one colour keeps one
  meaning, but it must be read), a line joining a pick's places (reads as a route nobody knows), the names alone
  in colours (markers without a name get nothing), and numbers in coloured circles. What it costs: no single
  colour means "picked" any more (the heading PICKED is in the headings' grey, and the dev icon keeps the old
  cyan, `Kept`), soft colours stand out less from the map than the strong cyan did, and with red-green colour
  blindness several of the eight look alike: the names, and the rows' order, still tell them apart.
- **Nothing else steps back for picks.** Every other quest marker stays at full strength (the owner's "Still it
  should show all other quest markers"); only pointing at something steps the rest back ("Stepping back"), and
  then picks don't step back either: they are the plan for this raid, as much as the ways out.
- **The rail.** A map's card shows its picks first, as a group of their own headed PICKED (in the headings' grey), then COMPLETE
  and PROGRESS without them, with the effort hairlines drawn anew (`Planning.Sections`). A pick that only
  progresses here keeps its note ("2 of 5 objectives here"). Compared on 2026-10-03 with picks sorted first inside
  COMPLETE and PROGRESS: there they scattered over two sections (a progress-only pick ended up at the bottom), while
  a group of their own says "this raid's work" at a glance. BRING lists what the picks need first, then a hairline
  and the rest (`Planning.BringOrder`). A map with picks is suggested first, most picks first: the player's plan
  before the planner's. That holds whatever the map's rank: the maps with picks all come first, then every other
  map with work on it in the planner's order (`RaidPlanner.Rank`; until 2026-10-09 the planner's best filled up to
  four, and until 2026-10-04 the list was cut to four first, so a pick on the map ranked fifth never showed). A folded card
  shows its picks' glyphs first, each in its pick's colour, then a hairline. The raid card starts with PICKED, nearest first, and
  NEXT is the nearest objective among the picks.
- **Zero picks is the app as it was:** no PICKED group, no reordering, no line on the map.
- The study log records `pick` and `unpick` (with how: pen, done, …) and `picks.clear`, when it is on.

### Extract requirements

Shown under each extract in the Raid rail, short, in gold, with the item's icon when one is handed over:

| from | example |
| --- | --- |
| `transferItem` in roubles, dollars or euros | "Pay 5,000 ₽" (car V-Ex), "Pay €2,400" (Icebreaker heli) |
| `transferItem`, any other item | "Hand over Note with code word Onyx" (secret extracts) |
| internal name `Alpinist*` / `RedRebel*` | "Red Rebel ice pick and paracord, no armored rig" (Cliff Descent, Mountain Pass, Climber's Trail) |
| "(Flare)" in the English name, or `sniper` as a word of the internal name (`E9_sniper`, `customs_sniper_exit`) | "Fire a red signal flare there" |
| "(Co-op)" in the English name | "Co-op: a PMC and a player Scav leave together" |
| `switches` | "ZB-013 Power Switch first"; a switch tarkov.dev lists on most of a map's extracts is only shown where its English name contains the extract's English name (it lists the ZB-013 switch on every Customs extract) |
| transit `conditions` | "TerraGroup Labs access keycard required (1)" |

Internal extract names are kept from the payload before translation (`GameData.ExtractKeys`); transit conditions
are translated although the payload's translation list misses them. **The rules never read the name in the game's
language** (review of 2026-10-04): they looked for "(Flare)" and "(Co-op)" in the translated name, and German has
"Mira-Allee" for "Mira Ave (Flare)", so the rule was lost there. The loader keeps each extract's and switch's
English name by id beside the translated one (`GameData.EnglishNames`), and `ExtractRules` reads those and the
internal names only; a test runs every exit of the cached data through the rules in English and in German and
expects the same. `sniper` counts as a word of the internal name, between underscores, not anywhere in it:
Customs' "Sniper Roadblock" is an ordinary exit and was told to fire a flare.

### Keyboard (window focused only)

| key | action |
| --- | --- |
| F | show my position, once |
| Shift+F | Follow my position on / off |
| + / − | zoom in / out (following, about the player); "+" with or without Shift, and the number pad's keys |
| 0 | show the whole map (following stays on: the next position centres on you at that zoom) |
| PgUp / PgDn | show the floor above / below |
| Esc | close the cards (it never drops picks) |
| F1 or ? | help |
| Ctrl+, | settings |
| → or Space, ←, Esc | while the tour is up (*The tour*): the next chapter (Enter too), the one before, end it. Every other key does nothing then; with the tour closed, Enter is the focused button's |

Keyboard accelerators sit on the window root with their placement hidden; WinUI would otherwise show the first
one's key as a tooltip over the whole window. "+" is a key of its own on some keyboards and Shift with "=" on
others (US); both are one virtual key, and a shortcut without Shift never saw the second, so "+" didn't zoom on a
US keyboard (`ZoomKeys`; review of 2026-10-04).

**The rail's rows by keyboard** (Down and Up through the rows, Enter as a click on one, P as its pen; the review of
2026-10-04, E6: the linked highlight needed a pointer) were removed on 2026-10-09 (owner): this section itself said
nothing in the app needed them. The tour keeps Enter as its next chapter, and Up and Down do nothing while it is up;
otherwise those keys go to whatever has the focus (a focused MAP list switches the map with its arrows, as a list
does).

Mouse: drag to pan (following stays on: the next position brings the view back), wheel to zoom at the cursor
(following with the view on the player, about the player), double-click to
zoom in, point at anything to see what belongs to
it, click a quest (in the list or on the map) to keep its card open, click its pen to pick it for the coming raid,
click the edge badge to show your position.

## 5. Quest taxonomy

Every objective gets one type, from tarkov.dev's objective `type`. The game gives each quest one hand-assigned
type on its Tasks screen (Elimination, Pickup, Exploration, Discovery, Completion, …); that type is not in any
public data and can't be derived reliably (a check against the wiki matched 62.5 %), so Shturmap types objectives
instead and borrows the game's names and look where one fits, so the labels feel familiar:

| type | glyph | objective types | in raid? |
| --- | --- | --- | --- |
| **Elimination** | crosshair (the game uses a skull) | `shoot` | yes |
| **Exploration** | magnifier (as in the game) | `visit` | yes, at a place |
| **Pickup** | open hand (the game has a hand) | `findQuestItem` | yes, at a place |
| **Place** | push pin | `plantItem`, `plantQuestItem`, `mark`, `useItem` | yes, at a place; needs an item |
| **Find in raid** | box | `findItem` | yes, anywhere; found in raid (FIR) only where the data's `foundInRaid` says so |
| **Survive** | runner | `extract`, `experience` (an in-raid health condition, not XP) | yes |
| **Trader** | handshake | `giveItem`, `giveQuestItem`, `sellItem`, `buildWeapon`, `traderLevel`, `traderStanding`, `playerLevel`, `skill`, `taskStatus`, `dialogue`, `globalVariable`, anything new | no |

A quest's type is that of its most common in-raid objective type (ties: Elimination, Pickup, Place, Exploration,
Survive, Find in raid); a quest with no in-raid objectives is a Trader quest. This is derived, not the game's own
label. Never use the game's icon artwork; the glyphs only echo it.

**The types' icons are filled shapes from Phosphor Icons** (MIT; `THIRD-PARTY-NOTICES.md`), chosen by the owner on
2026-10-04 from a panel of six sets, each tried in the real app: "B Solid Other Pictures but with the exploration
icon from A Solid and with Survive icon from A solid". Until then they were thin outlines from Windows' icon font
(Segoe Fluent Icons) and a crosshair of the app's own; inside a map marker, where the glyph is about 10 px, those
were faint, and filled shapes read at that size. All seven are the family's "fill" weight, except the magnifier,
which is its "bold" one (a filled lens reads as a dot). Each is the family's path as published, in `Glyphs`, drawn
filled and fitted by its own bounds: 15 px in the lists (`KindGlyph`), the marker's glyph size on the map. One
drawing serves both, so the two can't drift. Requirements keep Windows' font: a key glyph `E8D7` for keys, a
briefcase `E821` for items to bring, the padlock and the power symbol on the map; on Windows 10 that font has to
be installed for those (README).

The type is named after the common case. A `findItem` whose item may be bought (`foundInRaid` false) keeps the type
and its glyph, but nothing says "found in raid" of it: the planner, the plan's order and the item card go by the
data's `foundInRaid` (§7; the review of 2026-10-04 found the planner treating every `findItem` as found in raid).

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

- **Keys**: each objective's `requiredKeys` (each entry a list of alternatives, shown as "A or B"; several entries
  are several doors), and of the quest's `neededKeys` for that map only the keys none of its objectives names.
  tarkov.dev's quest-level list is flat: per map, the keys the quest's objectives name (in its data of 2026-10-04
  exactly that, for all 58 PvE and 57 PvP quests with keys), so it can't say whether two keys are both needed (the
  dorm room and the cabin of one quest) or either will do (two keys to one room); the objectives' lists can. Until
  2026-10-04 both were listed, so "Room 306 key" and "Room 308 key" stood as two rows where either opens the room,
  and an objective's line repeated every key of the quest (`RaidPlanner`'s requirements, `Planning.QuestOnlyKeys`).
  A key needed alone also serves any "A or B" row that holds it: that row goes, and its quests are named on the
  key's row ("key for Quest A, Quest B"; they used to vanish with the row).
- **Bring**: items a Place objective consumes (`items` of `plantItem`, `markerItem` of `mark`, `useAny` of
  `useItem`, the quest item of `plantQuestItem`), with counts summed per item.
- Shown aggregated per map in Plan, and inline on the objective in Raid ("Key: Dorm room 114 key", "Bring: MS2000 Marker"),
  on every objective that needs it.
- Shturmap cannot see the stash; it lists what is needed, not what is missing.

## 7. Raid planner

For each map (variants sharing artwork, like Ground Zero 21+, count as one), using active quests only, and of each
quest the objectives that are left: the logs don't report objective progress, so every objective counts except
those the player ticked as done (`Planning.Open`; §4, "Quest cards", *Ticks*):

- An objective is **doable** on a map if it names that map or has a place there, or names no map and is Kill,
  Collect or Survive. It is **tied** to the map if it names the map or has a place there.
- A quest counts for a map only if at least one of its doable objectives is tied to it; work that fits any map
  doesn't argue for one map over another.
- A quest can be **finished** on a map if all its in-raid objectives are doable there, none needs items found in
  raid (they depend on luck; a `findItem` whose item may be bought, `foundInRaid` false, doesn't count as one, the
  same cut as "Plan order" below) and no kill count is above 3; otherwise it is **progressed**.
- Score = 3 per finishable quest + 1 per tied objective with a place + 0.6 per tied objective without one + 0.1 per
  untied doable objective. Maps are ranked by score, and every map with work on it is shown, the best (or the one
  on screen) expanded. Maps with picked quests come before them, whatever their score (§4, "Picks"). Until
  2026-10-09 only the top four were shown, plus every map with a pick (decided 2026-10-04, from the review); the
  owner then had every map with quests listed, as the planner's cut left maps out without a word on screen. MAP's
  list holds the same maps with the same counts.
- Quests whose in-raid work fits any map are listed once under **Any map**.
- No walking time (owner, 2026-10-03: "~17 min walking" didn't say which quests it was for, players rarely just
  walk, and it was an estimate shown as a figure). A map card's line under the name holds facts only: the raid's
  length and its bosses with their chances ("40 min raid · Kaban 75% · Kollontay 75%"). The planner still measures
  a nearest-neighbour route through one place per located objective, only to break ties between maps of equal
  score; it is never shown.
- A **progressed** quest's row says why it can't be finished there, from the same tests (`Planning.ProgressNote`):
  "2 of 5 objectives here" (its other in-raid objectives are on other maps; hand-overs and optional ones don't
  count), "needs items found in raid", "25 kills in all" (a kill count above 3; the total the data gives, since the
  logs don't report kill progress). Joined by " · " when several apply.
- A key an objective names is shown on that objective's line. A key only the quest lists (§6) is shown on its
  objectives that have a place on that map, not on hand-ins or extracts.

### Plan order

**Owner, 2026-10-03:** the order of the quests in Plan wasn't clear (it was the order the quests were stored in).
Each section, COMPLETE and PROGRESS, is now ordered by what the quest's work on that map involves, from little to a
lot, then by complexity, then by the quest giver, then by name (`QuestEffort`, applied in `RaidPlanner.Plan`).

- **Effort groups**, named by the activity, not "easy" or "hard": the data supports facts (what an objective asks),
  not judgements (how hard a player finds it). A quest's group is that of its most demanding objective counted for
  the map (in-raid, not optional, as the planner counts them):
  - **Go there**: `visit`, `mark`, `plantItem`, `plantQuestItem`, `useItem`, `findQuestItem`, a `findItem` that may
    be bought, an `extract` that doesn't ask for the status "Survived" (whether it names an exit or not: the status
    is what `QuestEffort` looks at).
  - **Find or survive**: `findItem` found in raid, `extract` with the status "Survived", `experience` (staying under
    a health effect), and kills of Scavs or sniper Scavs without conditions (targets `Savage`, `Marksman`,
    `assaultGroup`, `Any`; `assaultGroup` is a Scav type: tarkov.dev's English text for it is "Scav").
  - **Fight**: kills of PMCs (`AnyPmc`, `Bear`), Rogues (`ExUsec`), Raiders (`PmcBot`), Black Division
    (`blackDivision`, `pmcBotBlackDiv`), the Labyrinth guards (`tagillaHelperAgro`), cultists (`sectant…`), bosses
    and their guards (`boss…`, `follower…`, `infected…`), and any kill with a condition set: a weapon or weapon mods,
    body parts, a distance (tarkov.dev writes 0 or null for none), gear worn or not worn, a time of day, a health
    effect on the player or the enemy, or a zone (an area of the map). A target the rules don't know counts as a
    fight, the safe side. In PvE, "kill PMCs" means AI PMCs, easier than players in PvP; they stay in Fight, the
    riskiest kind of objective either way.
- **Complexity**, within a group: a tuple compared in order, no weights: how many of the quest's objectives count
  on the map, how many kill conditions they set, and the largest kill count in buckets (none, up to 5, up to 15,
  more).
- Then the **trader**, in the order tarkov.dev lists traders (the game's own: Prapor, Therapist, Fence, …), then the
  **name**.
- Only tarkov.dev's structured fields count: the objective `type`, `targetNames`, `count`, the kill-condition
  fields, `exitStatus`, `foundInRaid`; never the description, and no AI. Kill targets and exit statuses are
  translated by tarkov.dev, so the loader keeps their keys from before translation (`GameData.ObjectiveFacts`): the
  order is the same in every language. New quests sort by themselves.
- **Display:** a thin hairline above the first row of each later group, no headings; the quest-type glyph shows
  why a row is where it is. One line in help: "Quests are ordered from going somewhere, to finding or surviving,
  to fighting; simpler first within each."
- **After a tarkov.dev or game update** run `shturmap-cli effort pve` and `… regular`: every Plan row (all quests
  active) in the plan's order with its group and complexity, kill targets the rules don't know (counted as fights;
  name them in `QuestEffort`) and objective types they don't know. On 2026-10-03: 614 PvE rows (Go there 214, Find
  or survive 134, Fight 266) and 620 PvP rows, no unknown targets or types.

## 8. Engineering

| project | role |
| --- | --- |
| `Shturmap.Core` | no Windows dependencies: screenshots, projection, floors, bearings, logs, raid tracker, quest progress, taxonomy, planner, quest synopsis. Rules without side effects, with one exception: `UnpackedCopies` deletes the 0.1.0 builds' leftover folders under %TEMP% ("Distribution") |
| `Shturmap.Game` | install discovery (BSG launcher and Steam are equal), log tailer, screenshot watcher, game settings |
| `Shturmap.Data` | json.tarkov.dev loader (ETag cache, translations), SQLite progress store, game art cache (portraits, icons) |
| `Shturmap.Map` | SkiaSharp drawing: artwork per floor, camera, renderer, map content, glyphs |
| `Shturmap.Session` | the coordinator: inputs in, one immutable `SessionSnapshot` out |
| `Shturmap.App` | WinUI 3 window; reads snapshots, never game files |
| `tools/Shturmap.Cli` | headless runner: locate, replay, data, render, watch, simulate, quests; the audits after an update: synopses, effort, bring, handovers, spawns (docs/UPDATES.md) |

Rules:

- Core logic is pure and unit-tested, on log lines written by hand; a few tests also replay real sessions, which stay
  on the developer's PC and out of the repository (§3, "Test fixtures").
- Every change keeps `.\eng\dotnet.ps1 test --solution Shturmap.slnx` green, including `SafetyTests`.
- Verify UI with `Shturmap.exe --snapshot <folder>` (renders the window and the map to PNGs, and writes what Copy
  diagnostics would copy to `diagnostics.txt`) or `shturmap-cli render`; never capture the user's screens.
  `--verbose` adds the app log's DEBUG lines; `--study` (developer builds) keeps a study log for one session; `--show-report` opens
  the Report dialog with an example and what's sent, `--show-crash` the question after a crash (for snapshots;
  snapshots send nothing); `--send-report <text> <folder>` is a release's delivery check (§8 "Reports"; with
  `--fake-game <folder>` it leaves the player's data and study log alone; its lines still go to the app log): a
  developer build sends one real report through the dialog's Send and saves the window, a release only opens the
  dialog with the text, and Send is pressed by hand (nothing is sent without a click, and a command line isn't one). For website media, `tools\fake-raid.ps1 -Window
  1600x900 -Scale 2` renders at a fixed size (the app's `--window`; the UI reads larger), in English
  (`--culture <culture>`: that run's language for everything, as if chosen in settings, without saving it; `de-DE` also
  for a language still being translated, `qps-ploc` the pseudo-language; `--switch-language <culture>` switches it while running, §8 "Developer aids") and at twice the pixel density (`--snapshot-scale`, sharp on high-DPI screens). `-GroupPick`
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

Releases live in the code repository's **GitHub Releases**, and **Velopack** installs Shturmap and keeps it up to
date (owner, 2026-10-03: "the releases live with the code in the release section of the project", with an
auto-update mode; Velopack chosen over MSIX, which Windows installs only with a trusted signature, the Microsoft
Store, and a hand-made updater). 0.2.0 is the first public release. The 0.1.0 builds given to friends were one
self-unpacking exe, without updates; they need the Setup once.

- **What players download.** `Shturmap-Setup.exe` (vpk's `ShturmapApp-win-Setup.exe` under the name the README
  gives; both are on the release, with Velopack's portable zip). It installs for the Windows user, without admin
  rights, into `%LOCALAPPDATA%\ShturmapApp`, with a Start-menu and a desktop shortcut and an entry under Windows
  Settings → Apps. Measured 2026-10-03: the Setup is 106 MB, the installed app 244 MB (precompiled, like the folder
  build), a delta between two builds 1.7 MB.
- **The install folder is not the data folder.** Velopack installs to `%LOCALAPPDATA%\<pack id>`, and an uninstall
  deletes that folder. So the pack id is `ShturmapApp` (`Distribution.PackId`; `eng\release.ps1` packs with the
  same, a test checks), never `Shturmap`: the database, logs, cache, outbox and crash records in
  `%LOCALAPPDATA%\Shturmap` outlive the app. Checked 2026-10-03: installing, updating and uninstalling left the data
  folder's files all there and `shturmap.db` byte for byte the same; the uninstall removed the install folder, its
  shortcuts and its Apps entry.
- **Uninstalling from settings** (owner, 2026-10-03: "The installer should also be able to uninstall", then: an
  "Uninstall Shturmap…" in help, with the data only on a tick; in settings since help and settings were split). Windows' Settings → Apps (and the Start menu's
  Uninstall) already ran Velopack's uninstaller; the Setup itself has no repair-or-remove mode, and another installer
  around Velopack wasn't worth it. Settings' quiet link "UNINSTALL SHTURMAP…" shows only in an install Velopack made,
  the release (`ShturmapApp`) or the dev build (`ShturmapDev`) (`Uninstall.Offered`), never in a folder build. It
  asks one question, "Remove Shturmap from this PC?", with an unticked "Also delete my Shturmap data" and what that is
  (settings, quest history, logs, reports waiting, crash records, and for the release the download cache; the dev
  build's data is `%LOCALAPPDATA%\Shturmap-dev`, with its study log, and the cache it shares stays with the
  release's folder). UNINSTALL closes the session as RESTART NOW does (so a reinstall doesn't take the exit for a crash), leaves
  a note in the install folder when the box is ticked (`Uninstall.IntentFile`, removed when it isn't), starts
  Velopack's uninstaller (`Update.exe uninstall`, which shows a dialog only when something goes wrong) and ends.
  Velopack stops what still runs from the install, then starts the exe with its uninstall hook: `Program.Main`'s
  `OnBeforeUninstallFastCallback` deletes the data folder only on a note at most 5 minutes old, so an uninstall from
  Windows' Settings never deletes data, and retries for up to 20 s while the closing app still holds a file. The
  deletion is checked strictly (`Uninstall.MayDelete`): exactly this install's data folder (the release's
  `%LOCALAPPDATA%\Shturmap`, the dev build's `%LOCALAPPDATA%\Shturmap-dev`), directly in `%LOCALAPPDATA%`, not a
  link; never a `--data` folder, a parent, a child or another build's folder. Velopack's own hook rather than a
  script left behind: the deletion runs in Shturmap's code, after the app's files are let go, with the same check the
  tests cover. Developer check: `--uninstall-test keep|delete` (developer builds) runs it at once, before any
  session, with Velopack's dialogs off.
- **Updates.** `Program.Main` runs Velopack first: its Setup, updater and uninstall start the exe with their own
  arguments, and a version downloaded in an earlier session is applied there, before the app starts (the app then
  starts again with the same arguments). Then WinUI's own start (`DISABLE_XAML_GENERATED_MAIN`). An installed app
  asks GitHub at start and then every 6 hours, never more often (`UpdatePolicy`), anonymously: GitHub allows 60
  such requests an hour per address. A new version downloads in the background (a delta when there is one) and
  applies at the next start. One quiet line at the top of the Plan rail says "Update 0.2.1 ready: applies at next
  start", with RESTART NOW, between raids only: Shturmap never restarts by itself, and never during a raid.
  RESTART NOW starts Velopack's updater first, which waits for the app to end (a minute at most), applies the version
  and starts it; only then is the session closed and the app ended. Where the downloaded version is gone or the
  updater can't start, the session goes on and a notice says why ("The update couldn't be applied: its download is
  gone. Shturmap keeps running."), and a version whose download is gone counts as not downloaded (review of
  2026-10-09: the session was closed first, and Velopack's own restart returned without a word, leaving a window
  that drew nothing).
  "Updates" in settings: **Automatic** (the default), **Tell me only** (it asks; the line offers DOWNLOAD), **Off** (no
  request at all). A version already downloaded applies at the next start whatever the setting.
  **When applying fails** (owner, 2026-10-05: "it tells me always that the update is available even though i
  downloaded and restarted"). Velopack applies a version by moving the install's `current` folder aside, and that
  fails while another program holds one of its files: it tried for 10 s, gave up and started the old version again,
  without a word (its log, `%LOCALAPPDATA%\velopack\velopack_<id>.log`: "being used by another process").
  **The cause that day: a browser Shturmap had started.** Velopack starts the app in the install's `current` folder,
  and what the app opens inherits its working folder: Firefox, started by a wiki link while it wasn't running, worked
  in `ShturmapDev\current` (with its crash helper) and held the folder for as long as it ran, so every update failed,
  with or without the game. No file in the folder was open, which is why Restart Manager named none; the processes'
  working folders did (read from their PEB). The game and a second Shturmap window were suspected first and ruled out
  by trying. So the app leaves that folder before its window opens: `Program` sets its working folder to the user's
  own, which nothing needs to move (`WorkingFolderTests`; every file beside the exe is found from
  `AppContext.BaseDirectory`). A browser an earlier version started keeps the old folder until it is closed.
  Velopack applies a downloaded version before every start (Program), so one still waiting when the app starts is
  one that couldn't be applied (`Updater.NotApplied`): the app log says so (WARN), a notice says it once, "Update
  0.3.1 couldn't be applied: another program kept Shturmap's folder in use, often a browser Shturmap opened. Close it,
  then restart Shturmap.", and the line says "Update 0.3.1 didn't apply: close what Shturmap opened, then restart",
  with RESTART NOW. Builds the Setup
  didn't install (the folder build, `dotnet run`) and developer runs (snapshots, fake games, the demo) ask nothing;
  help says "Updates: not available in this build". Checks, finds and downloads are logged at INFO; a failure is a
  WARN line and a retry at the next check, never a notice.
- **Pre-releases while in private testing.** Releases are GitHub pre-releases named "Shturmap <version> (private
  testing)" (owner, 2026-10-03: "we should state that the app is still in private testing"), so the update check
  includes pre-releases (`Distribution.PreReleases`); with the first stable release it becomes false. The README
  opens with the same note.
- **The network.** Asking for a new version is the one request besides tarkov.dev's data and art and the reports
  a player sends: `api.github.com` for the list, the packages from GitHub's release-asset hosts. Never Velopack's
  own update service (`SafetyTests`). Help, the README and PRIVACY.md say that GitHub sees the address, as with any
  download, and nothing else is sent.
- **Releasing.** `eng\release.ps1` builds into `artifacts\release` (owner, 2026-10-03: "The artifacts should contain
  a dev and release artifact"; the dev build is `artifacts\dev`, below): the folder `app\` (precompiled, with the
  Sentry DSN), and after asking GitHub for the last release so `vpk pack` can build a delta, `packages\` (the Setup,
  full and delta packages, `releases.win.json` and `RELEASES`, the portable zip), plus `Shturmap-Setup.exe` and its
  `.sha256`. The notes are `docs\release-notes\<version>.md`; what the What's New card says of the version is its
  section in `docs\whats-new.md` (up to five lines, built into the app; §4, "Screen anatomy"), written with the notes.
  `eng\publish-release.ps1` uploads it (`vpk upload github`, tag
  `v<version>`, a pre-release) and adds `Shturmap-Setup.exe` and its `.sha256`, all to a draft that it publishes once
  every file is on it (`-Draft` leaves the draft); it refuses unless the tree is clean, the commit pushed and the build
  made from that commit (`artifacts\release\app`). Both run in the Release workflow (next point) for every release
  players get; on the PC, `eng\release.ps1` builds for tries and update tests. `vpk` is a
  pinned local tool (`.config\dotnet-tools.json`). `eng\publish.ps1` builds only the folder (`artifacts\Shturmap`,
  what `tools\fake-raid.ps1` runs; it keeps the developer data folder). To test the whole update path without GitHub, `--update-feed <folder>` points
  an installed build at a local feed and lets it update even in a snapshot or a fake game. The folder must be on a
  fixed local drive (`LocalFeed`): no network share in either slash form, no device path, no mapped or removable
  drive, since an update is code that runs as the player (2026-10-04: `//server/share` passed as local).
  Velopack's Setup 1.2.161 crashes when given arguments for the app (`-- …`); install silently with `--silent` only.
- **Built and published on GitHub, with provenance** (owner, 2026-10-09: adopt every trust step that can be
  automated, so that players can check the project although they can't know its author). The Release workflow
  (`.github/workflows/release.yml`) makes every release players get. The owner's go is running it, on main only:
  `gh workflow run release.yml` (`-f draft=true` leaves a draft), or the Actions tab. On GitHub's Windows runner it
  checks the app's packages for known vulnerabilities (`eng\audit-packages.ps1`), runs `eng\release.ps1` with the
  DSN from the `SENTRY_DSN` Actions secret (it fails without one and never prints it), installs the Setup as a player
  would and takes it apart again (`eng\install-test.ps1`, next point), attests build provenance
  (`actions/attest-build-provenance`: SLSA provenance signed through Sigstore) for `Shturmap-Setup.exe` and what vpk
  uploads for the version (the Setup, full and delta packages, the portable zip), and only then runs
  `eng\publish-release.ps1`, so a release can be checked from its first minute. No package cache: it restores from
  nuget.org. GitHub attests only in public repositories on this plan: while the repository is private the step is
  skipped with a warning, so the first release that can be checked is the first built after it went public. A
  release published from the PC would have no provenance, which the README promises from 0.4.0: it isn't done.
  **Checking a download:** `gh attestation verify Shturmap-Setup.exe -R shturmap/shturmap` (the GitHub CLI, signed
  in), and `Get-FileHash` against `Shturmap-Setup.exe.sha256`; the README says how under "Check it yourself".
- **The install test** (owner, 2026-10-09, after the first release: nothing had installed a Setup before players).
  `eng\install-test.ps1` runs in the Release workflow after the build and before the attestation:
  - **Install:** the Setup with `--silent` on the runner; the installed app's version must be the commit's, with
    Windows' Apps entry and both shortcuts. If the Setup started the app, it is closed.
  - **Start:** the installed app once with `--snapshot`, on the release's data folder, with no update check and no
    report. It must save its window and exit, with no ERROR line in its log and no crash record.
  - **Uninstall:** Velopack's, as settings starts it (`Update.exe --silent uninstall`). The app, its shortcuts and
    its Apps entry must be gone, and the data folder must still be there.

  Any failure stops the run before anything is attested or published. `-f test_only=true` runs the build and this
  test alone (no delta; nothing attested or published), to try a change without a release. The script refuses on a
  PC where Shturmap is installed or has data. It doesn't cover the update from the last release: that is tried on
  the PC with `--update-feed` (above), as 0.3.0 → 0.4.0 was before 0.4.0 was published.
- **Its name doesn't matter.** WinUI looks for the app's resources (its compiled XAML) in `resources.pri` or
  `<exe name>.pri`, so the project names its PRI file `resources.pri`: named after the project, any other exe
  name made the window fail to load (2026-10-03, with the 0.1.0 single exe).
- **The 0.1.0 leftovers.** The single exe unpacked about 200 MB per version to `%TEMP%\.net\Shturmap…\<id>`. The
  installed app removes those once, in the background (`UnpackedCopies`, logged): only folders of that shape
  holding `Shturmap.dll`, each moved aside before it is deleted, which Windows refuses while an old exe still runs
  from it; a later start tries again only if one was in use.
- **Unsigned.** Windows SmartScreen asks once about the Setup ("More info → Run anyway"); the README and the
  release notes say so. Updates don't ask. Free signing from the SignPath Foundation is to be asked for once the
  repository is public (docs/NEXT.md, "2026-10-09: trust"); until it is granted, no text says it.

### Data folders

Only the installed release keeps the player's data folder, `%LOCALAPPDATA%\Shturmap`; every other build keeps its
own, `%LOCALAPPDATA%\Shturmap-dev` (owner, 2026-10-03: the release installed beside the dev build must not share a
database, settings, app log, study log or reports with it; developer starts were mixing into the owner's study log).
"Other builds" are the dev build, the folder build (`eng\publish.ps1`, what `tools\fake-raid.ps1` runs), `dotnet
run`, the CLI and tests. `--data <folder>` picks any folder (the app and the CLI; the CLI reaches the release's data
with `--data "%LOCALAPPDATA%\Shturmap"`). The app chooses at start, before anything is written: Velopack's id of the
install (`ShturmapApp` → the release's folder, anything else → the developer folder; `Distribution.DataFolderFor`,
`AppPaths.Use`). Diagnostics and the app log's first line name the folder ("Data folder: dev"). If Velopack itself
can't start, where the exe runs from says which install it is (`%LOCALAPPDATA%\ShturmapApp\current`;
`Distribution.InstalledIdByFolder`), so an installed release keeps the player's folder for that run, without
updates, and the app log says so (review of 2026-10-04: it used to open the developer folder, and the player's
history seemed gone).

**A settings file that can't be read** (review of 2026-10-09). When SQLite finds `shturmap.db` damaged, or no
database at all, the session moves it aside with the time in its name (`shturmap.db.unreadable-20261009-1530`; its
`-wal` and `-shm` go with it), opens a fresh one, writes a WARN line and says for 30 s "Shturmap's settings file
couldn't be read and was set aside: picks, ticks and settings start over." The quest history comes back from the
game's logs, as at every start. Such a file used to stop the session from starting: the window stayed up without
quests, positions or a word. A file that is only out of reach for now (another program holds it, a lock that doesn't
pass, a folder that can't be written) is never moved; that session keeps its settings in memory and says "Shturmap's
settings file couldn't be opened: picks, ticks and settings aren't kept this time." A second Shturmap opens the same
file as the first (SQLite's WAL), and Windows moves no file another Shturmap holds open (`SettingsFile`).

The download cache (tarkov.dev's data, map artwork, the pictures drawn from it, portraits and icons) stays shared in
`%LOCALAPPDATA%\Shturmap\cache`, so nothing downloads twice. Two Shturmaps can write it at once: every download goes
to a temporary file of its own (`CachedHttp.TempFor`, the process id and a GUID) and replaces the cached file in one
move, retried for a moment while another process reads it (`CachedHttp.Replace`); a reader never sees half a file,
and a copy that can't be saved is fetched or drawn again next time. A test runs eight writers at once.

### Developer aids

**The dev build.** Beside the release, a dev build that is always current and never taken for the release (owner,
2026-10-03: "a dev artifact of the app that always carries the recent updates and is in dev mode … The dev version
should carry a different icon so it is visually clear"). `eng\dev.ps1` builds it into `artifacts\dev`; run it after
committing app changes (CLAUDE.md).

- **Dev mode.** Built with `ShturmapDev=true` (Release configuration, not precompiled: builds come often and deltas
  stay small), which defines `DEVTOOLS`, as Debug builds do (`Directory.Build.props`): the window says "Shturmap DEV"
  (`App.Title`), the exe, windows and taskbar show the dev icon, the build kind is "dev build", and the developer
  tools and the study log (§8, "Study log") compile in. Releases never define it.
- **Its own install.** Velopack's id `ShturmapDev` (`Distribution.DeveloperPackId`): `%LOCALAPPDATA%\ShturmapDev`, shortcuts
  "Shturmap DEV" on the desktop and in the Start menu, its own Apps entry. Its data is the developer folder above.
  It never mixes with the release's `ShturmapApp` (a test checks the ids and folders).
- **Always current.** The dev build updates itself from a local feed, `artifacts\dev\feed`, never from GitHub: each
  `eng\dev.ps1` run packs a new version (`<version>-dev.<UTC time>`, so each is newer) with a delta from the last,
  keeps the three newest versions in the feed (and in its lists), and writes the feed's folder to `dev-feed.txt` in
  the dev install's folder. The installed dev app reads that file at start, asks the feed (at start and every 6
  hours, `UpdatePolicy` as for the release), downloads in the background and applies at the next start, never during
  a raid; RESTART NOW applies it at once. No folder of the developer's PC is compiled in, and the dev app follows
  whichever checkout built last. The first run installs it (`--silent`); `artifacts\dev\Shturmap-DEV-Setup.exe`
  installs it on purpose. Checked 2026-10-03: two builds in a row, each found in the feed within a second, a 0.2–0.3
  MB delta downloaded in about 4 s, applied at the next start.
- **What changed.** Each dev build carries `dev\changelog.json` beside the exe: `build` (the full commit), `built` (UTC
  time) and `commits`, those since the last release tag `v*` (or the last 50), newest first, each `hash` (short),
  `date` and `subject` (the commit's title; no authors, no bodies, a user folder masked). The dev view shows it, so
  the owner can check whether a change is live and what changed lately ("No changelog in this build" without it).
- **Reports.** None by default (no DSN). `eng\dev.ps1 -WithReports` builds the DSN in; its reports carry the Sentry
  environment "dev" (`ReportEnvelopes.EnvironmentOf`), so they can be filtered apart.
- **A new game build** (owner, 2026-10-05: the patch notice). The game's log folders carry its build
  ("log_2026.01.01_15-00-00_1.1.5.1.47510"). A developer build keeps the newest it has seen (`gameBuild` in its
  settings), and when the logs come from a newer one, live or read back at start, it says once, for 20 s, "Game build
  1.1.6.0.48001 is new: time for the checks after a patch (docs/UPDATES.md)", with a line in the app log and
  `game.build` in the study log (`GameBuild`, `GameSession.NoticeGameBuilds`). The first build ever seen is only kept;
  an older one (a test server's install) is no news; snapshot and fake-game runs leave it alone. A release has none
  of it: the checks are the developer's.
- **The icon.** `brand\build.cs` makes `Shturmap-dev.ico`: the same mark, only recoloured, on the app's "kept" cyan
  (#3FD2E0) plate with the Ш in the plate's dark. `Logo.Dev` picks the variant; `.\eng\dotnet.ps1 run brand\build.cs
  -- dev-panel <png>` draws all three candidates beside the release icon (a cyan plate, a cyan band across the foot,
  an amber corner tab). No "DEV" lettering: a stencilled V beside the Ш and its chevron could read as the V of the
  war symbols the logo rules avoid (§4, "Logo").

**Continuous integration** (owner, 2026-10-05). Every push builds the solution in Debug (so the developer tools
compile too) and runs the tests on GitHub's Windows runner (`.github/workflows/ci.yml`), with the same
`eng\dotnet.ps1` as on the PC and the SDK global.json names. Tests that need the owner's PC skip there: the tarkov.dev
cache, the game's logs, real screenshots, a Release build, the website beside the repository. The repository is
public and so are the run logs: nothing a test prints may be private (CLAUDE.md). A failed run comes as GitHub's own
mail. The daily data check is designed in docs/NEXT.md, not built yet.
What else runs there (owner, 2026-10-09: every trust step that can be automated). After the tests, CI fails on a NuGet
package with a known vulnerability, direct or transitive (`eng\audit-packages.ps1`, nuget.org's advisories; also
when it couldn't check). That stands in for Dependabot: its pull requests would put commits by another identity into
the repository, whose only one is shturmap (CLAUDE.md), so a finding is fixed by hand. `RepositoryScanTests` reads
every tracked file for token shapes, a Sentry DSN, private keys, claude.ai links, paths in a user folder, email
addresses, and screenshot names or log sessions dated other than 2026-01-01, the one day examples use (owner,
2026-10-09: a real time of play with a position or a session's quests can be matched to one player), and names
file and line only. Every action in every workflow is pinned to a full commit with its version beside it. OpenSSF
Scorecard (`.github/workflows/scorecard.yml`) rates the repository's practices weekly and on
every push to main, publishes the result for the README's badge and puts its findings in the Security tab; it runs
only while the repository is public, which publishing and code scanning need on this plan. The Release workflow is
under "Distribution".

**The developer view** (owner, 2026-10-03: "a Dev view that allows to switch between modes and can fake-play a raid
that reacts to what I do in the app and 'fake-updates' the position on demand … not exposed in the production
version … to check the UI without having to play the game"). Developer builds only: its code is in `Dev` folders
(`src/Shturmap.App/Dev`, `src/Shturmap.Session/Dev`) wholly inside `#if DEVTOOLS`, and every use of it elsewhere
too. `DEVTOOLS` is defined for Debug builds and `eng\dev.ps1`'s `ShturmapDev=true` builds (Directory.Build.props,
with a Directory.Build.targets fallback for builds started without `-c`, whose Configuration the SDK only sets
after the props are read); a release never has it. `DevToolsGuardTests` check the wrapping, that no project
defines `DEVTOOLS` on its own, and that a Release build on the PC holds no developer-view type or member.
- `Shturmap.exe --dev-view` starts a session on a fake game folder of its own under `%TEMP%`
  (`shturmap-devview-<id>`, read like `--fake-game`, so nothing is sent and no study log is kept; folders older
  than a day go at the next start) and opens the view; F12 opens it in any developer build (a session on the real
  game offers a restart in the view). Chosen over switching a running session to a fake folder: re-pointing the
  session's tailer and watcher at runtime would touch the real start path for a developer aid.
- It writes what the game would: application-log lines (the mode, the scene line, the match setup or a local
  raid's transit line, the loading steps, the raid start and end, a transit, matching cancelled), push
  notifications (quests started, completed, failed; a group's map pick) and screenshot files named as the game
  names them (position, facing quaternion, raid clock). The app reads them through its real parser, tracker,
  watcher and session; tests read them back the same way. The logs say nothing of single objectives, so neither
  can the view.
- Positions on demand: with "Pick on the map" on, a click on the map is a screenshot there (a drag sets the
  facing), its height from the shown floor's band (or the nearest known place on the base map), adjustable; F9
  repeats the last position; "Age" makes the last position older without a new screenshot; "Record a path" and
  "Walk" take screenshots along clicked places.
- Triggers for what no file reaches: the Report dialog, the question after a crash, the update-ready line (which,
  as designed, stays hidden in a raid), a failed data load (offline, 404, 503; data back with "Reload data") and
  the "no game" notice.
- "What's in this build" shows `dev\changelog.json` beside the app (written by `eng\dev.ps1`: the build's commit
  and time, its commits newest first, with a filter: is a change in this build?), or "No changelog in this build"
  with the app's version and commit.
- `--dev-script <file>` plays the view's steps headless, one per line (`mode`, `map`, `side`, `hosting`, `load`,
  `steps`, `start`, `end`, `transit`, `quest start|complete|fail <id or name>`, `quest here <n>`, `pick <id or name>` (picks or unpicks a quest, as its pen does), `tick <id or name> <n>` (ticks or unticks its n-th objective as done, for the session only), `show <name>` (its card held, for a snapshot), `point [<id or name> [<n>] | item <id or name>]` (points at the quest, at its n-th objective or at an item as the pointer on its line would, and holds it for a snapshot; alone, at nothing again), `hover [<id or name> [<n> | cell | key]]` (the pointer on the quest's block in the lists and, inside it, on its n-th objective's line, its first need cell or a gold line that is a key, through the code the pointer's own events call; alone, it leaves the innermost of them, so a script can check what a mouse does with things that lie inside one another), `place <fx> <fy>
  [<fx> <fy>]`, `trail <x> <y> [<x> <y> ...]` (where the pointer has been in the main window, for the cards to tell where it is heading), `cards` (the open cards' titles into the app log, with its time), `pos <x> <y> <z> [yaw]`, `repeat`, `age <min>`, `walk <s>`, `trigger <what>`, `whatsnew [<n>]` (the newest version's What's New card, as help's link shows it; with n, the pointer on its n-th line, so its preview starts; alone, off it), `replay` (the made-up raid of What's New's preview replayed on Customs, as at a raid's end), `wait <s>`,
  `snapshot <folder>`, `exit`), for checks without clicking; `snapshot` also saves the view's two tabs.
  A step is followed by 0.3 s for the app to read what it wrote; the pointer's steps (`hover`,
  `point`, `trail`, `cards`) and `wait` write nothing and follow at once, since what they check lasts tenths of a
  second.

**Switching the language in a run** (developer builds only; 2026-10-10). `--switch-language <culture>` (`en-US`, `de-DE`,
`qps-ploc`) switches the language once the window shows the game data, exactly as a choice in settings does but for
that run only, and before any snapshot is taken (`--snapshot`); with `--culture` the run starts in one language and
ends in the other. The layout check compares such a run with a fresh one in the second language: every text must be the
same (docs/LANGUAGES.md, "Layout check"). The folder build (`eng\publish.ps1`, a release configuration) hasn't got it;
a Debug build and the dev build have.

### How the parts work

**Finding the game.** Candidates come from the `EscapeFromTarkov` uninstall key (HKLM 32/64, HKCU), the
launcher's `gamesRootDir` (older `gameRootDir`) and its subfolders, `C:\Battlestate Games`, Steam's own uninstall
key for the game (`Steam App 3932890`, the same three places), and Steam's `libraryfolders.vdf` +
`appmanifest_3932890.acf`. A candidate needs `EscapeFromTarkov.exe` or a `Logs\log_*`
session (also under `build`). The newest log session wins, at the start and at every look after it (every 30 s; "No
game", *It keeps looking*); all valid installs feed the quest backfill.

**Screenshot names.** `yyyy-MM-dd[HH-mm]_x, y, z_qx, qy, qz, qw_clock (n).png`; the trailing number is the in-raid
time of day when a position is present. Facing: `yaw = atan2(2(qx·qz + qw·qy), 1 − 2(qx² + qy²))`.

**Projection.** tarkov.dev's math: rotate (x, z) by `coordinateRotation`, `X = a·rx + b`, `Y = −c·ry + d`, SVG
fitted into the projected `svgBounds ?? bounds`. Golden test: a made-up Streets position, x 40 and z 120, lands at
(284.1, 417.3) in `StreetsOfTarkov.svg`.

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
then "Received notification"); only the first counts. A raid or load the log leaves open (no end line: the game was
closed or crashed in it) is closed without a length once the game has started again (a newer log session) or it
began longer ago than its map's raid length plus 30 minutes (two hours without a known length), at the last line the
log has of it, never at the next session's login line (`UnfinishedRaid`, `RaidTracker.CloseUnfinished`,
`RaidEnded.EndInLog`; "Screen anatomy", status bar). The raid's map is kept apart from the map on screen
(`SessionSnapshot.RaidMap`; "Screen anatomy", *The raid's map is not the map on screen*).

**Following the logs** (`LogTailer`). The newest log session's application and notification logs are polled (every
500 ms while they grow, every 2 s when quiet), at most 4 MB of a file per poll. What a log holds when it is first
opened at Shturmap's start is replay: it sets the state and shows no cue, notice or ping. When the game starts
again, the session before is read to its end first, under its own name: the rest of a log longer than one read, a
file added since the last poll, and its last line, which nothing follows any more. Skipped, a raid's end or a quest
message was missing for the rest of the run (review of 2026-10-04, A44). A file of the old session that can't be
opened puts the new session off for three polls at most. The LOGS light says "Logs live" while the game writes: a
line since Shturmap started following, within the last ten minutes; replay doesn't count, so a start with the game
closed says "Logs" (A43: it said "live" for ten minutes after every start). Most lines are no event, so the light
is looked at again every 30 s, with the open raid.

**Clock changes** (`WallClock`; review of 2026-10-04, A41). The log's times are local wall-clock times without an
offset, a screenshot's time and the PC's clock are local too, and one taken from another is an hour off across a
clock change, two nights a year. On the spring night a raid begun at 01:50 was 95 minutes old at 03:25 (it is 35)
and was closed as one that can't still be running; on the autumn night a raid begun at 02:50 and ended at 02:10 ran
for minus 40 minutes, and a session read back had its events sorted by those times, the end before the start. So
every span of time goes through one place, which turns wall-clock times into instants by Windows' rules for the
PC's time zone: a raid's length (the cue, the last-raid line), the minutes in the status bar, whether an open raid
can still be running, a screenshot against a raid's end, the study log's timings. What the player reads stays the
local time as written ("started 21:02").
The hour the autumn change repeats can stand for two instants, and a log's time doesn't say which. No reading is
fixed; what is known of the order settles it:
- **Two times in a known order** (a start and an end, a start and now): the reading in which time doesn't run
  backwards, and of two such the one with the least time between. A raid of 20 minutes rather than 80; and an open
  raid that began in the repeated hour counts from its later reading, since closing a raid that still runs is the
  worse mistake. A raid that really ran across the whole repeated hour reads an hour short: its two times alone
  can't say otherwise.
- **Two times close together in either order** (the insurer's note around a raid's end, a screenshot around it): the
  reading that puts them nearest.
- **The lines of one log**, whose times never go backwards: each takes the earliest reading that isn't before the
  line above, starting with the first pass through the hour, because only then is the step back to 02:00 seen for
  what it is. The events of several logs are put in order by these instants (`WallClock.Sequence`, in `LogTailer`).
- A time that is an instant already counts as that: the PC's clock and a file's time, which .NET marks with the side
  of the repeated hour they are on, and a quest message's server time.
A time in the hour the spring change skips, which no clock shows, is read as if the clocks hadn't moved yet.
Not covered: which of two log sessions is the newer goes by their folder names' times, and two sessions started
within the repeated hour could be told the wrong way round; the map's age tag, the position's age in the window and
"recent" screenshots at a start still take one clock time from another (`FixAge.Of` is there for them).

**Quest progress.** Only the game's log counts: quest started/failed/completed notifications, backfilled from every
log session on disk and followed live; newest wins. A message in a log session that names no mode counts for the
mode Shturmap is in, live and read back alike (review of 2026-10-09: the backfill left such a session's quests
out). Prerequisites of active or completed quests that strictly require "complete" are shown as implied, never
stored. A quest started before the oldest log on disk is not known.
Databases from earlier versions may hold Tasks-scan and TarkovEyes-import rows, or quest states once set by hand;
they are ignored (and could never reopen a quest the log saw completed). The logs say nothing of single objectives:
the only word on those is the player's own tick (§4, "Quest cards", *Ticks*), kept apart from the quest states, in
the settings. `shturmap-cli quests` lists active quests with every observation.
`shturmap-cli synopses [mode]` lists every Plan row's synopsis with its flags (§5, "Quest synopsis"), and
`shturmap-cli effort [mode]` every Plan row's effort group and complexity (§7, "Plan order"). What to run and look at after a
game patch or a tarkov.dev update, and what each rule assumes of the data and the game: docs/UPDATES.md.

**Floors.** The floor shown is the player's, from the height of the last fix. The picker lists floors that have
artwork of their own, top first, with a dot on the player's; a pick (click or PgUp/PgDn) holds until the next
screenshot. Floors without their own artwork (Customs' 4th, Reserve's upper floors) are drawn in the base layer.
An SVG map's floors are read ahead, in the background, as soon as the map is shown (`MapArtwork.ReadFloorsAsync`,
started by the map view): a floor's picture used to be read by the first paint that showed the floor, on the
drawing thread, which stood still for that long (the review of 2026-10-04). A paint never waits now: a floor that
isn't read yet is left out of that frame and drawn when it arrives (`FloorRead`), one that can't be read is left
out and said in the app log, and a snapshot waits for the floors. The CLI and the tests, which don't read ahead,
read a floor when they ask for it, as before.
On the maps without SVG artwork the floors are tarkov.dev's tile layers: the shown floor's tiles over the base
layer's, which dims as under an SVG floor (the sheet stands in with the same floors). Map labels with heights
(tarkov.dev's bottom/top) show only on their floor, as on tarkov.dev; labels without heights show on every floor.

**Item sources.** json.tarkov.dev `items` (17 MB; only trader offers, flea level and a last price are read),
`barters`, `crafts`, `hideout` (+ translations), fetched after the main data and refreshed daily; loose spawns come
from the maps payload's `lootLoose`. A download that fails for a reason that may pass (no connection, a timeout, a
server error, a page in place of the data) is asked for again by itself, after the waits of "Asking again" below,
until it loads or the mode changes; until 2026-10-04 one failed download left the item cards without sources until a
restart. A failure that won't pass by itself (data in a shape Shturmap can't read) is noted in the app log and not
asked for again in that session.
When the game language's texts arrive late (below), the sources load again in that language: station names are
part of the texts.

**Asking again** (`RetrySchedule`; owner, 2026-10-04: no unnecessary load on tarkov.dev). After a failure that may pass,
tarkov.dev is asked again after 2, 4, 8 and 16 minutes, then every 30 minutes for as long as it fails; a success, or the
player changing the game mode, starts over at 2. It was every 2 minutes without end: with tarkov.dev down, every running
Shturmap asked for up to 11 files every 2 minutes, about 700 rounds a day where there are now about 50. One schedule,
counted separately, for the game data, the game language's texts and the item sources. A try asks only for what is
missing or older than its keep time: the rest is answered from the saved copy. An answer 200 that isn't JSON at all (a
page a sign-in portal, a network filter or a CDN sends in place of the data, or a body that is empty or cut off) is
never saved (`CachedHttp`, json; owner, 2026-10-09). It counts as a failed download: a saved copy is used in its place
("Data (offline copy)") and is asked for again at its next keep time; with none the load fails as a page in place of the
data, not as unreadable (`LoadFailure.NotData`): it says nothing of tarkov.dev's format, and the page goes when the
player signs in to the network or leaves the filter, so it is asked for again after the same waits as a failure that may
pass. Until then such a page was saved like the data: it replaced a good saved copy (so once that was older than its
keep time the player saw "No game data", not the offline copy), failed as unreadable, which isn't asked for again, and
was read again at the next start for as long as it counted as fresh (review of 2026-10-09). A page saved by an earlier
version is forgotten when a load can't read it (`GameDataLoader.ForgetWhatIsNoJson`), and that load fails as NotData.
JSON in a shape Shturmap doesn't know (tarkov.dev changed its format) stays saved and is revalidated as before, and
stays unreadable, not asked for again: downloading it again would bring the same, at every start of every Shturmap. A
failed download is never a reason to forget a good saved copy.

**The app's own language** (owner, 2026-10-10: multi-language support, English and German first, every language
tarkov.dev has to follow). **One language for everything**: Shturmap's own texts, its number and date formats ("Pay
5,000 ₽" and "25 Sep" in English, "5.000 ₽" and "25. Sep." in German, never Windows' formats inside another language's
sentences; review of 2026-10-04) and the game data's names all follow one choice (owner, 2026-10-10: "the app should
switch languages entirely, otherwise that makes it weird mixing the two"). `UiLanguage.Choose` makes it:
1. the player's choice in settings, "Language: Automatic · English · Deutsch", each language in its own name;
2. else the game's language (its settings' `Language`, `GameLanguage.Common`), when Shturmap's texts are complete in it;
3. else Windows' display language (the one Windows' own menus are in, read once at start before anything sets the
   app's), when they are complete in it; Windows' regional formats don't count;
4. else English.

A game language Shturmap's texts aren't written in yet (Russian, say) keeps the game data in it: the names stay as the
game shows them, and only Shturmap's own words take Windows' language or English, as before 2026-10-10, until that
language's texts are written (owner, 2026-10-10). The pseudo-language (`UiLanguage.Pseudo`, developer runs only;
docs/LANGUAGES.md) shows English game names. A language is offered, and chosen by itself, only once every text of it is
translated and reviewed (`UiLanguage.Supported`); one being translated (`UiLanguage.InTranslation`) shows only in a
developer run (`--culture de-DE`), English where a text isn't written yet. The language **switches while Shturmap
runs** (owner, 2026-10-10), from settings at once: every text on screen, the formats and the map's labels change, the
game data is loaded in the new language as when the game language's texts arrive late (below), and what was on screen
is the same as after a fresh start in that language (the layout check compares the two; docs/LANGUAGES.md). A new
notice is said in the new language; one already shown stays as it was said. Logs, the study log, diagnostics, reports
and the CLI stay English: they are read by whoever fixes Shturmap. The rules that read English data (quest synopses
§5, extract requirements, `QuestCards.SaysWhere`) still read only English; in another data language their rows show
tarkov.dev's sentence, as before.

How it is made (2026-10-10). The setting is `language` in shturmap.db: `auto` (or none) or a language's code; a value
that isn't offered (a language only a developer build offers, read by another build) counts as automatic. Windows'
display language is read once, as the app starts, before anything sets the app's culture. The session opens before the
window (`GameSession.Open`: the app's settings, the game and its settings), chooses and keeps the choice
(`GameSession.Language`), and the app sets Shturmap's texts to it (`UiLanguage.Set`) before the window builds them, so
a start shows no English first. A choice while running: the session chooses again and, when the game data's language
changes, loads the data and the item sources in a new round, as a change of mode does (what the old language still
waited for is dropped, the waits of "Asking again" start over, a load in the old language that ends later is dropped),
the old data staying on screen until the new is here and the maps on screen named anew. The window switches Shturmap's
texts on its own thread, reads every x:Bind again (`Bindings.Update`, the cards' too), sets again what code sets over
XAML from its state (the tour's chapter, the report dialog's links, help's legend, What's New, the question after a
crash), makes the rows drawn from templates anew, and the session composes its snapshot again
(`GameSession.RepublishAsync`: the plans and the screenshot keys' names with it). The snapshot takes the language in
use each time it is made (`UiLanguage.ApplyHere`): the current culture travels with async work, so a loop started
before a switch would sort and format in the old one.

**Texts.** Every word Shturmap shows is in a project's texts file, never in code or XAML: `CoreTexts`, `MapTexts`,
`DataTexts`, `GameTexts`, `SessionTexts`, `AppTexts` (the app's code), `ViewTexts` (its XAML) and `RuleTexts` (the
app's rules, which the Core tests compile too). Each is a `.resx` with the English texts and one `<Name>.<code>.resx`
per language. A build makes a class of each English file (`Directory.Build.targets`, `ShturmapTexts`): a property per
text, a method per text with placeholders, its parameters the placeholders' names in alphabetical order, called with
named arguments (`SessionTexts.ExitListRead(count: n)`), so a misspelt or missing text, or a placeholder left out, is
a build error. A text is looked up in the language in use when it is shown (`UiLanguage.Text`), never kept from
before, which is what lets the language switch while Shturmap runs; XAML binds to the classes (`{x:Bind
local:ViewTexts.HelpAndFeedback}`) and the window updates its bindings when the language changes.
- **Whole sentences with named placeholders**, never pieces glued together or a word chosen by code: each language puts
  the words where its grammar wants them. Plurals are part of the text, in the syntax of ICU's MessageFormat that
  translators know: `{count, plural, one {# extract} other {# extracts}}`, with Unicode's plural forms of every
  language tarkov.dev has (`PluralRules`: Polish and Russian need three or four, so code never picks "extract" or
  "extracts"). `TextFormat` fills them in; a text that doesn't parse shows as written rather than failing.
- **Game names stand where they don't change form**: "Bring: {item}", not "Bring the {item}"; Russian and Polish
  change a noun's ending with its place in the sentence, and the names come from tarkov.dev as they are.
- **Numbers** in a placeholder are written in the language's format; a number that needs grouping (prices, "5,000 ₽")
  is formatted by the caller with `UiLanguage.Culture`. Dates and times likewise.
- **A comment on each English text** says where it shows and what its placeholders are, for the translator.
- **Each translated text says which English text it was made from** (its comment: `en: <the English text>`). The
  translation tests (`TranslationTests`) check every file: the English texts parse, a translation has the English
  text's placeholders and its language's plural forms, and was made from the English text as it is now; a language
  that is offered has every text. So changing an English text means translating it again in the same commit, reviewed
  as below (docs/LANGUAGES.md, "Translating"); changing a translation alone needs only the review.
- **Not translated**: internal values (settings' stored values, ids, file names, log lines), and the words of the
  game itself that Shturmap reads (the extract list's header in a screenshot): those are matched in the game's
  language, not shown.

**Language.** The game data comes in the language chosen above (`LanguageChoice.Data`), translated by tarkov.dev's
`<payload>_<language>` files. The extract list's reader still reads the game's language, which is what the screenshot
shows: when the data is in another language, the maps' texts in the game's language are loaded beside it (`maps_<code>`,
about 25 KB, kept for an hour as the other texts), so an exit is matched by its name in the game's language, in English and in the language shown (`GameData.GameNames`); when they can't be had, by the other two, without a word,
until the data loads again. The game names some languages its own way, and tarkov.dev answers those with 404:
`GameLanguage.Common` maps `ge`→`de`, `cz`→`cs`, `jp`→`ja`, `kr`→`ko`, `po`→`pt`, `tu`→`tr`, `ch`→`zh`,
`es-mx`→`es` (2026-10-02: a friend's German game asked for `maps_ge`, and no data loaded at all), one table for
tarkov.dev and for the extract list's text recognition (review of 2026-10-09: each had a copy). A language
tarkov.dev still lacks falls back to English for everything, so the data stays one language and always loads.
Only "not found" (404) says the language is lacking. A translation that fails any other way (no connection, a
timeout, 5xx) with no saved copy is never said as "No German texts on tarkov.dev" (until 2026-10-04 any failed
request read as a missing language, so a moment's 503 left the session in English with an untrue notice). The data
then loads in English with the failure named (`GameData.LanguageFailure`), and the session says what is true:
"Showing English: the German texts couldn't be loaded. tarkov.dev answered 503. Shturmap tries again in 2
minutes." It asks for the texts again in the background, after the waits of "Asking again" above, until they load;
then the data, the map names on screen and the item sources change to the game's language, and a quiet notice
says "German texts loaded." The first fix of 2026-10-04 failed the whole load instead, which was true but left a
player with no data at all for as long as one translation file kept failing (review of the same day, A46). A
failure that won't pass by itself (tarkov.dev answers 403, say) is said with "Please report it." and not asked for
again in that session. A load that fails as a whole also looks at the downloads it never came to await, so their
failures don't turn up later as crash records.

**Study log.** Developer builds only (below), in their data folder: `%LOCALAPPDATA%\Shturmap-dev\study\yyyy-MM-dd.jsonl`, one JSON object per line: `t`, `src` (`game` or
`ui`), `ev`, event fields, and `ctx.*` (raid phase, map, raid minutes, age of the last fix) on every line, so UI
use can be lined up with raids and quest completions later. Game: app start/exit, data loaded (with the top
suggestion), mode, raid loading (with the suggestion rank of the map actually played and the bring list), raid
start/end, quest started/completed/failed (live only), each fix, each notice. UI: window focus/blur and pointer
in/out (with durations: the closest signals to attention), map pan (one per drag), zoom (one per wheel burst,
button or key), fit, follow, floor picks, map picks (picker or plan card), selections, hovers resting ≥ 0.4 s
(where: list, card, map), card open/hold/close (level, seconds open), help open/close, keys, rail scrolls, notice
dismissals. Added after the first study (2026-10-01): the plan's
COMPLETE and PROGRESS quest ids at raid load (to check which got completed), the evidence for each side decision,
screenshots that gave no position, positions that weren't shown (no raid in the log, or the raid's map not known),
whether a new position was in view and uses of the edge arrow and F, map
previews, notices expiring vs
closed, the quests visible in the rail and the rail's scroll position when the window gets focus, stale-position
banners seen (until the banner went, 2026-10-03), side switches, why a session started (the previous one ended cleanly or not, the build's time), and
active-quest count changes outside quest events. Added 2026-10-02: group picks (location, map, time variant) and
group ready / not ready / start; each raid's loading steps with their seconds since the scene line (in
`raid.start`); the raid clock in each fix; the insurer's notes (kind, location, item count). Added 2026-10-04:
cards closed by a step into a raid (`cards.raid`), whether help was closed by the player or by a raid, and the
status bar's words going or coming back with the bar's width (`statusbar.words`). Added 2026-10-07: the raid replay
(`replay.play` with how it was asked for, its positions and minutes; `replay.end` with why; `replay.skip` for a raid
with too few positions) and What's New (`whatsnew.show`, `whatsnew.point` with the line, `whatsnew.close` with how:
×, a raid, or the line from help); a quest completed joining the QUEST COMPLETE cue on screen (`cue.join`, with how
many it says). How a raid ended
(survived, killed) is in none of the allowed logs. The nearest thing is `raid.outcomeHint` `{ lostInsured: true }`:
the insurer's "lost" note came during the raid or within 5 minutes after it, on the same location (in the owner's
logs it came 17–20 s before the raid's end line). It is a hint for later studies of the plan's accuracy, never shown;
no note proves nothing, since gear may not have been insured. Only Shturmap's own window is observed; nothing is
sent anywhere.

The study log is **in developer builds only** (owner, 2026-10-03: "The study log should only be part of the dev
version and not be in the release version"; earlier that day it was the player's choice in every build, off by
default). In a developer build (the dev build, Debug builds: `DEVTOOLS`) it is on unless "Keep a study log" in
settings is unticked, with one line saying it is for developer builds, what it records, that it stays on this PC
(where) and that nothing is sent. The switch is saved in the app's settings (`studyLog` = `on`/`off` in
`shturmap.db`; a Debug build's `shturmap-cli study [on|off]` sets it without the app); `--study` keeps it for one
session without changing the switch; snapshot and fake-game runs never keep one. Days older than 30 are removed at
start, on or off. Ticking it mid-session writes `study.on`, unticking writes `study.off` and stops.

A release (and the folder build) compiles it out under `#if DEVTOOLS`: no switch in settings, no `--study`, no
setting read, no pruning, no line in diagnostics or the app log, so it never creates a `study` folder. The
`Study.Ui` and `Study.Game` calls stay in the code and do nothing there (`StudyLog.Available` is false, so `Enabled`
stays false and the writer is compiled out). Study days an earlier build left in `%LOCALAPPDATA%\Shturmap\study`
stay as they are; a release never reads, prunes or deletes them, and "Also delete my Shturmap data" removes them
with the rest of the folder. `DevToolsGuardTests.A_release_build_has_no_study_log` checks a Release build on the
PC (no switch, no `--study`, no setting key, no writer).

**App log** (owner, 2026-10-03). `%LOCALAPPDATA%\Shturmap\logs\shturmap-yyyy-MM-dd.log`: a short support trail,
not a trace. INFO: the start (version with commit, installed or folder build, Windows), the game, logs and
screenshots folders found, the game's language and screenshot key, mode, data loaded (from where, language, counts,
when checked), raids loading, starting, ending, the study switch (developer builds). WARN: something degraded but working (the data
from the saved copy, English instead of the game's language, a folder not found, the item sources or a map's
artwork not loaded). ERROR: what failed, with the exception. A step of the session's background work that fails (a
line of the game's log, one log session's quest history, a look for the game, a position) is one ERROR line per kind
of failure, and the work carries on (review of 2026-10-04: one database or file error used to end log following or
the quest history for the rest of the session, with no line and the LOGS light still "live"). DEBUG only with `--verbose` (and the website demo,
which times its clip by its lines): notices, the map surface, snapshots, fake games, the demo. Every line is
written with the profile folder as `%USERPROFILE%` (any case, either slash; that covers Documents, OneDrive and
AppData under it); any other folder directly under a `Users` folder (another account's, or the profile's short
"8.3" name) as `<user>`, the user's name as a folder elsewhere (Documents moved to another drive) as `<user>`, and
the host of a network path as `<host>` (`PathMask`; review of 2026-10-04: only the exact profile folder was masked);
no profile or account ids, no game file contents. At most 1 MB a day: a last WARN line says so
and nothing more is written until the next day. Seven days kept; the old `spotter-*.log` files are removed.

**Error messages** (owner, 2026-10-03). What the player sees says what failed in plain words, with the status where
there is one, and what to do; the exception goes to the app log only. Data (`LoadProblem`): "Couldn't reach tarkov.dev,
and there's no saved copy yet. Check the internet connection."; "tarkov.dev didn't answer in time…"; "tarkov.dev
answered 503. It is busy or down for a moment…"; "tarkov.dev answered 404. Please report it."; "tarkov.dev's data has
changed in a way Shturmap can't read. Please report it." (JSON in a shape Shturmap doesn't know); "tarkov.dev's answer
wasn't its data: a sign-in page or a filter in between? Shturmap tries again in a few minutes." (a body that isn't JSON
at all, with no saved copy to use: a sign-in page, a network filter's or a CDN's page sent with 200, an empty or cut-off
body; owner, 2026-10-09: it was said as unreadable, which blamed tarkov.dev, asked for a report that couldn't help, and
wasn't tried again); "Couldn't save tarkov.dev's data on this PC…". It comes as a notice (30 s) after "No game data.",
the DATA chip says "No game data" and its tooltip says why. A failure that may pass (no connection, a timeout, 5xx or
429, a page in place of the data) is tried again after growing waits ("Asking again", above: 2, 4, 8, 16 minutes, then
every 30), said once; the notice names the wait that follows it ("Shturmap tries again in 2 minutes; if it keeps
failing, please report it."), and the tooltip, which stays up while the tries go on, names none ("tries again by itself,
at first after 2 minutes, then less often"). A page in place of the data asks for no report, in the notice or the
tooltip, and its notice has no REPORT link: what sends the page is between the PC and tarkov.dev, where a report can't
change it ("…a filter in between? Shturmap tries again in 2 minutes."). A download whose answer starts and then stops
counts among them: after 30 s of silence it is a timeout, and a connection that breaks off mid-answer is "couldn't
reach", not a disk problem (`CachedHttp.BodyIdleLimit`; the client's own timeout ends with the headers, so until
2026-10-04 such a download never ended and "Loading game data…" stood for good). A notice that asks for a report carries
a REPORT link to the Report dialog (COPY DIAGNOSTICS in a build that can't send). With a saved copy the data loads from
it (DATA chip "Data (offline copy)"). No texts in the game's language: a quiet notice, "No German texts on tarkov.dev;
showing English." Game not found, or found without its Logs folder: the line in place of the Plan card says so, for as
long as it lasts, and no notice (§4, *No game*). A map's artwork that doesn't download: "No map artwork for Customs:
couldn't download it; check the internet connection. A 10 m grid stands in…", once per map; a tile render that can't be
had: "No map render for The Lab: …", the same way.

**Diagnostics** (owner, 2026-10-03). Help ends with quiet links: COPY DIAGNOSTICS, LOG FOLDER ↗ (opens
`%LOCALAPPDATA%\Shturmap\logs`), PRIVACY ↗ and LICENCES ↗. Copy diagnostics puts plain text on the clipboard and says
"Diagnostics copied: paste them into your message.": Shturmap's version with commit and whether it is the single
exe; Windows' version and build; the install type (Steam, BSG launcher, manual) and whether the game, logs and
screenshots folders were found; mode; the game's language, Windows' display language, the language in use with where
it came from (the setting, the game, Windows, the default; "this run only" for "--culture") and the game data's, and
tarkov.dev's as loaded (with "extract names also in de" when the game's own names were loaded beside it); the data's
state (loaded, offline copy
or failed with the kind, status and plain reason, and when it was checked); the active quest count; whether "Delete
position screenshots" is on (§2); the study log
on or off (developer builds); and today's last 200 app-log lines. Paths are masked, every 24-digit id (profiles, accounts, quests) is
cut to `<id>`, and no quest names or lists go along (`Redact`). Copying sends nothing; a report carries the same
text when the player keeps "Include diagnostics", shown first.

**Reports** (owner, 2026-10-03: "as easy as possible … one coherent easy to use thing, otherwise people won't
bother", and crash reports as a mode the player chooses; Sentry, EU region; crash reports "Ask after a crash" by
default). One way to report, from the app, with no account and no browser: no GitHub issue forms, no separate web
form. Problems and ideas both go through it.
**GitHub Issues beside it** (owner, 2026-10-09: "Keep github issues on"; "mention GitHub Issues in the README and
website"). The repository's issues stay open as a second, public route for players with a GitHub account. The README,
the website and the release notes name them after the app's own button, and say that issues are public, so the
game's logs, screenshots and ids stay out of them (the app's report leaves those out). The app itself points to its
button only. One issue form (`.github/ISSUE_TEMPLATE`; owner, the same day: "add the issue template") says it is
public before anything else, asks for a problem or an idea, what happened or would help, and the version, and can't be
sent until "I left out the game's logs, my screenshots and ids from my game" is ticked. Blank issues are off, so
every issue goes through it, and the app's report is offered beside it.

- **The Report dialog.** The feedback button at the top right, the REPORT link on notices that ask for a report, and
  ADD A NOTE after a crash report was sent all open it. PROBLEM | IDEA (the placeholder follows: "What happened,
  and what did you expect?" / "What would help, and when would you use it?"), the text (required, at most 4,000
  characters), a contact for a reply (optional, e.g. a Discord name or an email, at most 120), "Include
  diagnostics" (on; SHOW WHAT'S SENT shows exactly what goes: kind, text, contact, version and Windows, and the
  diagnostics as attached), one line on where it goes with a Privacy link, CANCEL and SEND. Cancel keeps the text
  for the session; Esc closes it; the map's keys don't fire while it is open. After Send it says "Sent. Thank you.
  Report 3f2a91c0." (the id the player can quote), or that it couldn't go now and is kept to go by itself at the
  next start, or that it was refused and is kept on the PC. It never loses the text: a report is written to
  `%LOCALAPPDATA%\Shturmap\outbox` before it is sent and deleted once it has gone; the outbox is tried again at
  each start; a refused one is kept as `.refused.txt` and not tried again.
- **Crash reports.** Unhandled exceptions (the UI's, a background thread's) and unobserved task exceptions are
  written on the PC at once as crash records in `%LOCALAPPDATA%\Shturmap\crashes` (JSON, masked): the exception
  chain (outermost first, five at most) with each frame's module, method, file name (never its folder) and line;
  version, build kind, Windows; the last 50 app-log lines. The same exception is written once; errors the app
  survives at most three times a session, the same one once. A native crash (Skia, ANGLE) or a kill leaves no
  exception: each running Shturmap holds `crashes\running-<session>.json` open and a clean end deletes it; at the
  next start a marker no Shturmap holds, from a session that left no record and from after Windows last started
  (before that it was a shutdown), becomes a "closed unexpectedly" record with that session's last log lines. The
  next start then does what "Crash reports" says (`crashReports` in `shturmap.db`): **Ask** (default) shows the one
  question ("Shturmap closed unexpectedly last time. Send a crash report? It helps to fix it."; for errors it
  survived, "ran into an error"), with SEND, DON'T SEND, ALWAYS SEND (sends and switches the setting) and WHAT'S
  SENT (the records as text, exactly what goes); closing it asks again next time. **Always send** sends at the
  start and logs it. **Never** keeps them on the PC only. A record the player said Send to that couldn't go yet is
  sent at the next start without asking again, unless the setting is Never by then: choosing Never takes back an
  earlier Send too, at once and at the next start, and such a record stays on the PC (2026-10-04: approved records
  went out before the setting was read). A sent one is deleted; records are kept 30 days, twenty at most.
  Never sent: memory dumps, screenshots, game files, ids, quest lists.
- **How it goes out.** Sentry (Functional Software, Inc.), EU data region. Only the SDK's event types and envelope
  format are used: `ReportEnvelopes` builds the envelope from what the player saw and `ReportSender` posts it, so
  nothing is added on the way (no machine name, installation id, IP, breadcrumbs, sessions or tracing) and the
  SDK's automatic client, which would capture on its own, is never created (`SafetyTests`). A problem or idea is
  Sentry user feedback (contact as email or name) with the diagnostics attached as `diagnostics.txt`; a crash is an
  event with the exception chain and frames (cause first, caller first, so Sentry groups the same crash; an
  unexpected exit is one group) and `log.txt` attached. Tags: kind (problem, idea, crash), report id, build kind,
  source; release `shturmap@<version>`; the time it happened as an extra. The Sentry package also writes the build
  folder into referencing assemblies (`Sentry.ProjectDirectory`), which holds the developer's user name;
  `Directory.Build.targets` turns that off, and `SentryNative` is off. Its source generator copied every build
  property, the project folder among them, into `Shturmap.dll`: `SentryDisableSourceGenerator` is on. And every
  DLL carried its PDB path, with the user name in it: `PathMap` in `Directory.Build.props` maps the repository to
  `/_/`. A release build's files were scanned on 2026-10-03 and none holds the user name; scan again after adding a
  package (the release must carry nothing of the developer's machine).
  **No address and no place.** Sentry sees the sender's internet address as any server does, and derives a town and
  country from it even with the project's "Prevent Storing of IP Addresses" on (its scrubbing docs; a report's "User
  Geography" showed the owner's town, 2026-10-09). So every report says `infer_ip: never` in its `sdk` and carries an
  empty `user.ip_address` (null, which Relay takes as "don't infer"; `ReportSender.Sealed`), and the project has an
  Advanced Data Scrubbing rule, [Remove] [Anything] from [`$user.geo.**`], which Sentry documents as the way to drop
  what it derived (added by the owner, 2026-10-09). IP storage is off and the default scrubbers are on
  (owner, 2026-10-09).
- **Where to.** The DSN is not in the repository: `eng\release.ps1` and `eng\publish.ps1` pass it from the untracked
  `eng\sentry.dsn` (gitignored) or `SHTURMAP_SENTRY_DSN` (which the Release workflow sets from the `SENTRY_DSN`
  Actions secret) as an assembly attribute. Only `https://…sentry.io` is
  accepted (or this PC over http, for tests). A build without one (developer builds, forks) shows the Report link
  greyed with "Reporting isn't set up in this build: copy the diagnostics and send them to whoever gave you
  Shturmap."; notices then link COPY DIAGNOSTICS. Snapshot, fake-game and demo runs never send anything.
- **Said honestly.** Help, the README and THIRD-PARTY-NOTICES say "Nothing is sent unless you send a report or
  allow crash reports". `PRIVACY.md` (bundled as `privacy.txt`, opened by PRIVACY ↗ and the dialog's Privacy link)
  says what a report and a crash report hold, who receives them, why (consent: Send, or Always send), how long and
  how to ask for deletion: a report from the app that quotes the report id. Short and plain on purpose (owner,
  2026-10-09: "Short and plain"): reports go to "Shturmap's maintainer", kept at Sentry for at most 90 days, with no
  legal name, no published address and no legal-template wording.
- **Checking a release.** `Shturmap.exe --send-report "<text>" <folder>` opens the Report dialog with the text; press
  Send: the dialog must say "Sent. Thank you.". A release never sends on a command line alone (review of
  2026-10-04: any shortcut or program could have made an installed Shturmap send a report); only a developer build
  sends by itself, saves the window to the folder and exits. Crash reports are only tested against the
  local stand-in (`FakeSentry` in the tests), never sent for real.

## 9. Status

- Done: discovery, watchers, raid tracking, map with floors, player, facing, trail, extracts, transits, quest
  markers, objectives by distance, log backfill, live quest events, safety test,
  self-contained publish, taxonomy, raid planner, requirements, raid line, help panel, keyboard shortcuts,
  occasional-position UX (fix age beside the marker, compass directions), kit reminder on raid load (cue pictures, CHECK YOUR KIT), linked
  highlighting, quest cards (hover, held, nested), item cards with sources and loose
  spots, trader portraits and item icons, floor picker, study log, Tarkov-style visual language.
- Next: read a study log from a real session and correlate it with quest completions (`shturmap-cli` command).
- Named **Shturmap** (owner, 2026-10-01; was Spotter): Shturman, the navigator, plus map, and a word of its own
  so a search finds the app rather than the Woods boss. The old data folder and database move over on first start.
- The Lab, Labyrinth and Icebreaker are drawn from tarkov.dev's tile renders (§3, "Maps without SVG artwork";
  2026-10-03), as a sheet when no tile can be had (2026-10-02).
- 0.1.0 went to friends as one self-unpacking exe (2026-10-03). From 0.2.0, the first public release, a Setup from
  GitHub Releases that keeps itself up to date (Velopack; §8, "Distribution"): 106 MB to download, 244 MB installed.
- Reports and crash reports from the app, through Sentry (2026-10-03; §8, "Reports").
- Releases built, attested and published by the Release workflow on GitHub, the package audit and the repository
  scan in CI, pinned actions, OpenSSF Scorecard, and the README's "Check it yourself" (2026-10-09; §8,
  "Distribution" and "Continuous integration").
- Objective progress: by ticks the player sets on the quest card (2026-10-04; §4, "Quest cards", *Ticks*), since
  the logs don't report it.
- "Delete position screenshots" (2026-10-04; §2): off unless ticked, the one thing Shturmap changes outside its own
  folders.
- Open: the installed size (budget 80–120 MB, needs trimming). Declined (owner, 2026-10-04): editing quests by hand,
  opening with the game or with Windows, a text size setting. Declined (owner, 2026-10-07): shortcuts for zooming and
  panning that work while the game has the focus. They would need a global hotkey (§2), and in a week of the study log
  only about a fifth of the times Shturmap took the focus during a raid were for the view alone; over half were for
  quest rows and cards.
- The tour at a first start, with first-time lines for the first raid and position (2026-10-09; §4, "Screen anatomy",
  *The tour*).
- What's New, a card in Plan's rail with each line previewed on the map, and the raid replay in the RAID OVER cue with
  REPLAY on the last-raid line (2026-10-07; §4, "Screen anatomy"; "Map drawing", *The raid replay*). Designed and
  deferred (docs/NEXT.md): marks for what the player has. The QUEST COMPLETE cue and a completed quest's places
  ringing out (2026-10-07; §4, principle 11; "Map drawing").
- Removed (owner, 2026-10-09, the pre-release review): popped-out quest cards (§4, "Quest cards"); the keyboard's
  steps through the rail (§4, "Keyboard"); the raid clock's decode at each minute ("Screen anatomy", *The readout*).
