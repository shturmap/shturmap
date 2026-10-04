# Next: more from the game's logs

Plan for the next session (written 2026-10-01, evening). The owner asked for items 1–4 below; they come from a
read of everything in their own application and push-notification logs (20 sessions, 2026-08-15 to 2026-10-01).
Item 5 (one design system) was added on 2026-10-02, item 6 (no game installed) on 2026-10-03. Items 1, 3 and 4 are done
(2026-10-02), item 5 on 2026-10-03; 2 and 6 are open.
`docs/DESIGN.md` stays the binding spec: update it in the same change as each item.

## Review of 2026-10-04: findings (owner: "Add all findings to docs/NEXT.md and start fixing them")

A review of the code, the design's consistency and hands-off rule, the design elements, missing features, and the
visual language with its linked highlight. The test suite was green (614 tests); the views were checked in the app's
own snapshots (Plan, a raid, a held quest, The Lab, a local raid, a Scav raid). Each item says what is wrong, where,
and the direction of the fix. **Status** is one of: open, in work, done (with the date), owner to decide, needs a run
(a claim from reading the code that a test on a PC has to confirm first). Paths are under `src\`.

**Status after two rounds of fixes (2026-10-04):** the items marked done below are merged, with tests (the suite
went from 614 to 899); DESIGN.md has each change. The second round built D1 (ticking an objective) and E1 (the
highlight down to the objective). The owner then decided the open questions (2026-10-04): P1 (no game logs in the
repository; the history rewrite is the owner's to run), P2 (say it precisely), A5, A21, A27, B4, H3, C3, C5, C9 and
E2 are done as decided, and D3 (deleting position screenshots) was added and built. H4 and C1 were shown as before
and after pictures and merged on the owner's word, and B14 was measured and decided: it stays as it is, said
precisely. The owner ran the history rewrite (P1) and force-pushed it.

**Third round (2026-10-04; owner: "start with the caching check. Go on with the smaller open review items"):** the
caching check (B15) and some 25 of the smaller items, by five agents in worktrees of their own and the main session:
A17, A20, A32–A36, A39, A40, A42–A46, B2, B3, B5, B9, B11, B13, C2, C4, C5, E3–E5, H7, H9, and C6 for symbols of
different rank. The suite is at 1,063 tests. Found on the way and fixed: the raid card's lines stood in Segoe UI
and white instead of the app's typeface; a Scav zone's ring lay over the boss's octagon; "Sniper Roadblock" on
Customs was told to fire a flare. The leftovers followed the same day (owner: "Go on with the small leftovers"): A37, A41, C6, E3's boss names,
E6, H5 and A40's settings text; the suite is at 1,111 tests. Still open: A10 and A26 (need a run). The website's
wording followed the same day (owner: "build the website if something needs updating and push it"): the installer
and running programs, the delete setting, the three tile maps, ticking an objective, and where the game is
installed, each in the README's words.

**To try with a mouse** (built and checked through the developer script's `hover` and `point` steps, which call the
code behind the pointer's events; no real pointer moved over them): a raid card's objective line, a need cell, a
gold "Key: …" line and a folded card's glyph, and a click on each (where their cards open); a quest's name clicked
at its start (A3); closing the window with Alt+F4 while the pointer rests on a quest (A47).
From the third round, with a real mouse and keyboard: "+" on a US layout (Shift and "=") and on the number pad;
Esc in a popped-out card with a nested card open; with another map picked in the MAP list, a click on a quest row
and on a pen in the open Plan card (the map must stay), then on the map's row in the list (it switches); a card
held in Plan while a raid starts (it closes); the window narrowed to 900 px in a raid, and the three lights'
tooltips; moving from a quest's row onto its card and across its gaps and headings (the quest stays lit), and from
a BRING row onto the item's card (locks and loose squares stay); a padlock on the map (its quests take the weaker
tint); a second copy of the game started while Shturmap runs (it follows the newer logs, with a notice).
From the leftovers: Down and Up through the rail's rows in Plan and in a raid, Enter on a quest row, an item row
and a map row, P on a quest row, Esc; the mouse moved after stepping (the highlight jumps to what is under it) and
left resting over the rail while stepping (the keyboard's row keeps it); typing P and Enter in the Report dialog
(they must stay text); the MAP list opened (its arrows choose a map; closed, they step through the rows now);
Enter on a focused button with no row active (it must still click); a boss's name in the raid line and under a
map's name in Plan (its spawn zones light up, and a zone tints the name); "ALL 15 ↓" in the EXIT row (the list of
ways out comes to the top of the rail); two ways out at one place on the map, each with its own tooltip and click.

**Words for the owner to check, from the third round:** "Newer game logs in <folder>: quests and raids follow that
game now."; "NO POSITION YET · PRESS PRTSC OR MOUSE3" (status bar, in a raid only); "CLICK ITS ROW TO PLAN IT"; the
lights' tooltips when their words are hidden; the legend's new rows (the ping, a pick's cyan padlock, the gold
chevrons) and "for the first 45 seconds a cone shows…"; "No game data. … Shturmap tries again in 2 minutes; if it
keeps failing, please report it."; "Showing English: the German texts couldn't be loaded. tarkov.dev answered 503.
Shturmap tries again in 2 minutes."; "German texts loaded.".
From the leftovers: help's four new key lines (↓ / ↑, ENTER, P, ESC); "ALL 15 ↓" and its tooltip; settings'
"Updates: the updater couldn't start this time, so Shturmap isn't asking for new versions. Its log says why (LOG
FOLDER, below). Running Shturmap's Setup again repairs it; your data stays."; the README's new header.

**Found while fixing:**
- **A47. The app ended with an access violation when it exited while the map was pulsing** (a quest pointed at): the
  pulse's timer asked for a frame while the drawing surface was taken down. Every exit path now stops the window's
  timers and the map's drawing first (`MainWindow.StopForExit`). Status: done (2026-10-04; a script that exits while
  pointing at a quest ends with exit code 0, before 0xC0000005).
- **A43.** The LOGS light says "Logs live" for ten minutes after every start, also with the game closed: reading the
  old session at start counts as activity (`LogTailer.LastActivityUtc`, `GameSession.LogsHealth`). Status: done
  (2026-10-04: only what the game writes while it is followed counts).
- **A44.** A log longer than one read (4 MB) is cut short when the game starts a new session before it is read
  through: the rest of the old session is skipped. Status: done (2026-10-04: a session is read to its end first).
- **A45.** With A6, a picked quest that has work on five or more maps gives more than four map cards. Status: done (2026-10-04): kept and said, a
  pick's maps are always listed (Plan shows the maps as a short list).
- **A46.** With A31, a translation file that keeps answering with a server error on a first load means no data
  until it recovers (a notice and a retry every two minutes), where it used to fall back to English with an untrue
  notice. Decide whether English with a true notice would serve better. Status: done (2026-10-04): the data loads in English with a notice that
  says so truly, and the game language's texts are asked for again in the background, with a growing wait.
- **Words for the owner to check** (new with A1 and A2): "Got a position, but the game's log shows no raid, so it
  isn't shown."; "Got a position, but Shturmap can't tell which map this raid is on, so it isn't shown."; the
  last-raid line's "end not in the log"; the map's label "LOOKING AT WOODS · THE RAID IS ON CUSTOMS · YOUR NEXT
  POSITION SHOWS IT AGAIN"; the raid card's title "MAP NOT KNOWN".
- **D1 changed the README's sentence on quest tracking** ("Nothing has to be ticked off", with what a tick is
  for). The website says the same since 2026-10-04.
- **Words for the owner to check, from D1:** the tick's tooltip, "Done · ticked by you, 4 Oct" on the card, "DONE"
  on the raid card's line, and the new paragraph in help.

### P. Before the repository is public

- **P1. The fixture scrubber missed ids.** `tools\make-log-fixtures.ps1` masks profile ids only after "profileid";
  the committed log fixtures kept ids on `[Transit]` lines and the push channel's ids in the notification logs.
  Fix: mask every id that isn't public game data (quests, traders, item templates), scrub the fixtures, and a test
  that fails on any id that is neither public nor a placeholder. Then rewrite the history, as on 2026-10-02, before
  the repository is made public (owner's go needed: it changes every commit). Status: done in the tree (2026-10-04, owner: "I don't think logs should be in the repo at all if not needed"): the game
  logs are out of the repository (ignored by git, kept on the PC; the replay tests skip without them). The history
  was rewritten and force-pushed by the owner the same day: no commit holds `tests/fixtures/logs` any more, and a
  check of every commit against the list of the real ids found none. Left for the day the repository goes public:
  GitHub can keep the old commits reachable by their hashes for a while, so ask GitHub's support to remove them, or
  publish from a fresh repository. Every commit's hash changed with the rewrite.
- **P2. Velopack's updater lists the running processes.** Its `Update.exe` imports `EnumProcesses`, `OpenProcess`,
  `QueryFullProcessImageNameW` and `TerminateProcess` and logs "Checking for running processes" (it closes what runs
  from the install folder, at install, when an update is applied, on RESTART NOW and at uninstall). README and
  DESIGN.md §2 say Shturmap never opens a handle to the game and checks no processes; `SafetyTests` reads `src\` only.
  Which access right it asks for is unchecked. Status: reworded (owner, 2026-10-04): README "The installer and running programs", DESIGN.md §2, help, the release
  notes, and the website since the same day; the updater opens every process with the rights to read its path and
  to end it. Open: applying updates without looking at other processes.

### A. Code

The first twelve are the ones a player meets.

- **A1. A map picked in the MAP list during a raid takes the raid with it.** The next position, the status bar, the
  raid card and RAID OVER go to the picked map (`Shturmap.Session/GameSession.cs`: `MapForFix`, `SelectMapAsync`).
  Fix: the raid's map comes from the tracker; a pick in a raid is a look, and the raid's map is back with the next
  position. Status: done (2026-10-04; a map picked in a raid is a look, and a label on the map says so).
- **A2. A raid whose end never reached the log stays "in raid".** After a game crash or Alt+F4 in a raid, the next
  start shows "IN RAID · … · 1,310 MIN", and later a day-long last raid (`Shturmap.Core/Raid/RaidTracker.cs`,
  `Shturmap.Session/RaidStatus.cs`). Fix: an open raid that can't still be running (older than the map's raid length,
  or followed by a newer log session) is closed as "end not in the log", with no length. Status: done (2026-10-04; closed once it is older than the map's raid length plus 30 minutes, 2 hours when the
  length isn't known, or when a newer log session starts).
- **A3. A held card closes on the next mouse move** when the click was on the left of its row: the 240 px rule
  measures from the card alone (`Shturmap.App/Controls/CardStack.cs`: `PointerAt`). Fix: measure from the card and
  the row or marker it opened from. Status: done (2026-10-04).
- **A4. "Distances from your screenshot 1 min ago" stays at 1 min** until the next snapshot
  (`Shturmap.App/MainWindow.xaml.cs`: `RaidFixNote`). Fix: set it with the clock. Status: done (2026-10-04; it says the age the status bar says).
- **A5. Follow my position switches itself off for good on any drag**, also between raids with no position on the
  map (`Shturmap.App/Controls/MapView.cs`, `MainWindow.xaml.cs`: `FollowStopped`). Fix now: a drag or fit with no
  position on the map leaves following alone. Owner to decide: whether a drag in a raid should switch it off for
  later raids too (today), or only until the next raid starts. Status: done (2026-10-04; owner: only the toggle turns following off, a drag or a zoom is free, and the next position
  centres the view on the player at the current zoom).
- **A6. A pick on a map outside the planner's best four never shows** (`Shturmap.Core/Planning/RaidPlanner.cs`:
  `Rank` cuts to four before `Shturmap.Session/Planning.cs`: `Suggest` puts picks first). Status: done (2026-10-04; every map with a pick is shown, also beyond four).
- **A7. The Lab, Labyrinth and Icebreaker opened offline stay a sheet until restart**: once the tiles count as
  unavailable, nothing asks for them again (`Shturmap.Map/MapScene.cs`: `IsSheet`, `MapRenderer.Render`). Status: done
  (2026-10-04; asked for again every 30 s).
- **A8. BRING's source line can need the quest it serves** ("Ragman LL2 · 41,283 ₽ · after Dandies" for the beanie
  Dandies asks for; `Shturmap.Session/ItemCards.cs`: `Sources`). Fix: an offer that needs a quest the log hasn't
  seen completed comes after every other way. Status: done (2026-10-04; barters behind a quest too).
- **A9. One unexpected notification ends log following without a sign**: the tail loop catches only file errors,
  the parser throws on shapes it doesn't expect, and the backfill has no catch (`Shturmap.Game/Logs/LogTailer.cs`,
  `Shturmap.Core/Logs/GameLogParser.cs`). Also: what is already in the log at start must all count as replay, the
  last line and anything past the first 4 MB included. Status: done (2026-10-04).
- **A10. An ordinary Windows shutdown with Shturmap open likely reads as "closed unexpectedly"** at the next start:
  with Fast Startup the tick count isn't reset, and nothing handles the Windows session ending
  (`Shturmap.App/App.xaml.cs`). Status: needs a run (shut down with the app open, then start it).
- **A11. Popped-out cards all close, and are forgotten, when the mode changes** (the data is empty for a moment;
  `MainWindow.xaml.cs`: `RefreshPinned`); closing the main window may save an empty list the same way. Status: done
  (2026-10-04; a card whose quest isn't active in the mode shown closes but stays saved).
- **A12. The quest card's BRING doesn't add up counts** ("MS2000 Marker" where Plan says "×3";
  `Shturmap.Session/QuestCards.cs`: `Needs`). Status: done (2026-10-04).

Smaller, in the map:

- **A13.** In a raid, picked markers are drawn under other quests' markers (`MapRenderer.Render` groups by step-back
  measure). Status: done (2026-10-04).
- **A14.** A previewed map's zoom limit stays after the preview ends (`Camera.Restore`). Status: done (2026-10-04).
- **A15.** The scale bar averages both axes: up to 27 % off on Icebreaker (`MapRenderer.Scale`). Status: done (2026-10-04).
- **A16.** The tile cache (192) is smaller than a 4K view needs, which reloads without end; a tile that doesn't decode
  counts as missing for the session (`MapTiles`). Status: done (2026-10-04; the view's own tiles are never dropped, and a saved tile that isn't an image is
  thrown away).
- **A17.** While anything is pointed at, the whole map is drawn again about 60 times a second; a row rebuilt under the
  pointer keeps its focus (`MapView`, `Linked`). Fix: stop when the window isn't active, let go when the row goes.
  Status: done (2026-10-04): the focus, and the layout is kept between frames while nothing it is made
  from changes (how much a frame gains wasn't measured).
- **A37.** A cluster is left out when its middle place is out of view though others are in view; the hazard hatch
  runs over a zone's whole box; an SVG floor is read during the first paint that shows it. Status: done (2026-10-04): the cluster; the hatch was clipped already and now draws only its lines in view; floors are read ahead, off the drawing thread.
- **A38.** "A switch listed for every extract links nothing" uses a strict rule where `ExtractRules` uses "most".
  Status: done in the map (2026-10-04); `ExtractRules` still counts by itself.

Smaller, in planning and data:

- **A18.** Every `findItem` objective is planned as found in raid, also where the data says it may be bought
  (`RaidPlanner`: `Plan`, `WhyProgress`). Status: done (2026-10-04).
- **A19.** A download that stalls after its headers never ends: "Loading game data…" for good
  (`Shturmap.Data/Http/CachedHttp.cs`). Status: done (2026-10-04; 30 s without a byte ends it).
- **A20.** Another install with a newer log session (a test server, a leftover install) is followed for the whole
  run; discovery decides once (`Shturmap.Game/Install/InstallLocator.cs`, `GameSession.LookAgainAsync`). Status: done
  (2026-10-04): discovery runs every 30 s for the whole run, another install's newer log session is followed with a
  notice, and a folder the player chose is never left.
- **A21.** "N min left" is the map's raid length minus the time since the raid's start line: computed, and wrong
  after a reconnect or for a local Scav raid shown as a PMC's. Status: done (2026-10-04: "40 min raid · started 21:02").
- **A31.** The session's background loops (backfill, following, looking again) catch only a cancel: one database or
  file error ends them silently. Translation downloads left unwatched after a failed load become crash records.
  Status: done (2026-10-04).
- **A33.** Key rows: an "A or B" row dropped because A is also needed alone doesn't hand its quests over; quest-level
  keys are listed one by one even where they are alternatives. Status: done (2026-10-04): the data's quest-level list is exactly
  the objectives' keys as one flat list, so a key an objective names is that objective's row, with its alternatives
  ("306 or 308" as one row), and a key needed alone takes over the quests of an "A or B" row it satisfies.
- **A34.** "(Flare)" and "(Co-op)" are looked for in the translated extract name, so the rules are lost in another
  game language (`Shturmap.Session/ExtractRules.cs`). Status: done (2026-10-04; the rules read the English and internal names. Found on
  the way: Customs' "Sniper Roadblock" was told to fire a flare).
- **A35.** CHOOSE… and FIND AUTOMATICALLY read every log session on the UI thread. Status: done (2026-10-04).
- **A36.** Closing isn't safe to run twice (`ProgressStore`, `GameSession.DisposeAsync`); the uninstall path can do
  that. Status: done (2026-10-04).
- **A41.** Log times are local without an offset: an hour off across a clock change. Status: done (2026-10-04): spans of time go through one place (WallClock); a raid
  that runs across the whole repeated autumn hour still reads an hour short.

Smaller, in the app:

- **A29.** Cards have no largest height: a long quest's card is cut at the window's edge. Status: done (2026-10-04).
- **A32.** A scene's key is set before its artwork arrives, so a second snapshot can fill the old scene. Status: done (2026-10-04).
- **A42.** A held card stays over the map through loading and the raid; "+" doesn't zoom on a US keyboard; a
  popped-out window has no Esc. Status: done (2026-10-04): cards in the main window close at each step into a raid,
  Shift with "+" zooms too, and Esc in a popped-out window closes the cards opened from it. The two keys are to try
  by hand.

Smaller, in privacy and distribution:

- **A22.** `--update-feed` (in release builds, for testing updates) takes `//server/share` as a local folder
  (`Shturmap.App/Updater.cs`: `LocalFolder`). Status: done (2026-10-04).
- **A23.** "Never" doesn't stop crash records already approved (`App.xaml.cs`: `HandleReportsAsync`). Status: done (2026-10-04).
- **A24.** Only the exact profile folder is masked in the app log: Documents on another drive, a network host's
  name and short (8.3) names pass (`Shturmap.Session/Redact.cs`). Status: done (2026-10-04).
- **A25.** Paths from maps.json and wiki links from tarkov.dev are used without checking the host or the scheme.
  Status: done (2026-10-04: downloads only to tarkov.dev's two hosts and GitHub's raw host, over https; a wiki link
  only when it is an https address on the wiki).
- **A26.** A copy unpacked from the portable zip offers the in-app uninstall; what Velopack then removes is
  unchecked. Status: needs a run.
- **A28.** `ScreenshotWatcher.WaitUntilCompleteAsync` (unused, from the removed OCR feature) would open a screenshot
  image; the watcher's rescan catches too little and isn't re-armed after the folder is recreated. Status: done (2026-10-04).
- **A30.** The User-Agent says "Shturmap/0.1" whatever the version. Status: done (2026-10-04).
- **A39.** `eng\publish-release.ps1`'s "built from this commit" checks can pass on a build from a changed tree;
  `--send-report` in a release sends without a click. Status: done (2026-10-04): `eng\release.ps1` refuses a changed
  tree (`-AllowDirty` for a local try) and leaves a note that `eng\publish-release.ps1` checks; in a release
  `--send-report` only opens the Report dialog with the text, and Send is pressed by hand. Neither script was run to
  the end for this (a release build takes the DSN and packs installers); the next release is their first real run.
- **A40.** If Velopack fails to start, an installed release silently uses the developer data folder. Status: done
  (2026-10-04): where the exe runs from then says which install it is, so the release keeps the player's folder,
  and the app log says that this run doesn't update. Settings say in that case that the updater couldn't start, where the reason is and that the Setup repairs it.

Wording: **A27.** Numbers and dates follow Windows' language inside English text ("5.000 ₽", "3 Okt" on a German
Windows); snapshots hide it, since they run in en-US. Status: done (2026-10-04: the formats belong to the app's language, English today, in one place, `UiLanguage`,
for the languages to come).

### B. The design's consistency

- **B1. Cyan has three meanings**: picks, Follow my position "on", the dev icon; the colour table gives "on" to
  amber. Fix: the toggle's "on" in amber. Status: done (2026-10-04).
- **B2. Green means "PMC extract" on the map and "any way out" in the rail**: EXIT and every extract row's distance
  are green, transits and Scav extracts included; a quest card's "ACTIVE" is green too. Fix: the rail's rows in the
  colour of their kind. Status: done for the ways out (2026-10-04); a quest card's "ACTIVE" is amber since the same day.
- **B3. Sand** is the player's, but floor badges and the squares for loose items use it too. Status: done
  (2026-10-04): a floor badge wears its marker's colour, a loose item's square is ink.
- **B4. "Primary text ≥ 14 px"**: the raid card's objective lines are 13 px, the status bar 12, directions 11, and
  about fifty places are under 12; "numbers in a monospaced face" isn't true either (figures are Bahnschrift).
  Status: done (2026-10-04; owner: "the 14px rule is not necessary"; the principle no longer says either).
- **B5. The legend has "a done objective"**, which can't happen yet (D1 makes it real), and lacks the gold chevrons
  of a pointed-at quest, a picked quest's cyan padlock and the ping. Status: done (2026-10-04).
- **B6. "Never ask" and "no modal dialogs"** have exceptions the principles don't name: the Report dialog, the
  uninstall and crash questions, the side switch. Status: done (2026-10-04).md).
- **B7. DESIGN.md housekeeping**: principles are numbered 1–8, 11, 9, 10, 12; the rail is 380 px in one place and
  384 in another; §9 still says "fading marker"; §6 says "needs Dorm room 114 key" where the app says "Key: …"; help
  says "pen" and "highlighter"; the quest card's wiki link lacks "↗"; a held card is replaced after 0.65 s from a
  list, not 0.4 s. Status: done (2026-10-04).
- **B8. Stepping back**: other quests' zones fall to about 35 % (the table says 62 % and 80 %), hazards aren't in the
  table, and a picked quest's doors step back like any lock. Status: done (2026-10-04).
- **B9.** The facing cone shows for 60 s, the cards' facing-relative directions for 45 s. Status: done (2026-10-04): 45 s for both.
- **B10. PRIVACY.md and README** still place reports in help, say Sentry keeps country and town (it is told not to),
  and don't say that tarkov.dev's image service sees which icons and map tiles are asked for. Status: done for
  PRIVACY.md (2026-10-04: the feedback button and settings, what Sentry is told, refused reports, a section on
  downloads; the owner still checks a received report in Sentry for an address or a location).
- **B14. On the three tile maps the tile requests follow the view**, and with Follow my position on, the view
  follows the player: the image service could tell roughly where on the map the player is, while DESIGN.md §2 says
  no position is sent anywhere. Fix: when such a map opens, fetch the whole map's tiles at the zoom levels used (they
  stay in the cache for a month), so later requests say nothing about the view. Status: decided (owner,
  2026-10-04): it stays as it is and is said precisely, in PRIVACY.md and in DESIGN.md §2 under "Never share a
  position". The fix as proposed was measured first: a whole map at every zoom level is far too much (The Lab:
  3,525 tiles a layer, about 290 MB, three layers; Labyrinth 4,486 tiles; Icebreaker 17 layers). Fetching each
  layer whole up to zoom 4 only (about 10 MB a layer for The Lab) would have ended the view-dependent requests,
  but the picture turns soft when zooming in further, and the owner didn't want that ("I would like to avoid
  that"); the same plus deeper tiles outside raids would have left soft and sharp patches side by side in a raid.
  The website says it too, where it says what is sent (2026-10-04).
- **B15. The caching check (owner, 2026-10-04: "Check if we properly cache the maps so we don't put unneccessary
  load on tarkov.dev").** Status: done (2026-10-04; DESIGN.md §3, "No unnecessary load on tarkov.dev", has the
  numbers). The cache works as meant: requests go out only at a start or a mode change, a saved copy within its
  age costs nothing, and all three hosts answer "not modified" to an older copy's ETag (tried against each). A
  start a day after the last is some 15 requests and about 3 MB compressed; the item sources are the largest part
  (1.9 MB, they change every few minutes, fetched once a day). Two wastes were found and fixed: a tile or picture
  the server doesn't have was asked for again in every session (now remembered for a week), and while the data
  couldn't be loaded every running Shturmap asked again every 2 minutes without end (the wait now grows: 2, 4, 8,
  16 minutes, then every 30). Left as they are, with the reasons in DESIGN.md: the hour for quests and maps, the
  30 days for tiles (the service says 7 days, but tiles are files that haven't changed since January 2025 and
  are checked with their ETag after the 30).
- **B11. DESIGN.md against itself and the code**: §5 calls every `findItem` found-in-raid while §7 has one "that may
  be bought" (A18); §7's "a named exit" isn't what `QuestEffort` tests; §5's Trader types lack `playerLevel`; §8's
  project table gives Core "name matching" (gone) and calls it pure (`UnpackedCopies` deletes folders); the Steam
  uninstall key isn't mentioned. Status: done (2026-10-04; the first point had gone with A18, the other four are
  corrected in DESIGN.md).
- **B12. Quest states set by hand** still have a code path (`SetQuestStateAsync`, "set by you") though the states
  come from the log alone and the owner declined editing quests by hand (below). Status: done (2026-10-04; old databases' rows of that kind are ignored).
- **B13.** A Plan card is one button: a click on a row or a pen inside an expanded card of a map that isn't shown
  also switches the map ("two clicks, two meanings"). Status: done (2026-10-04): the open card is no button;
  only a row of the map list switches the map.

### H. Hands-off

- **H1.** The window's place isn't remembered (D2). **H2.** Follow can't stay on (A5); off, a position out of view
  needs F, and F needs the window's focus. **H6.** The MAP list is live in a raid (A1).
- **H3. EXIT names the nearest exit for your side**, but on most maps the game opens only some exits, by where you
  spawned, and at the raid's start the nearest is usually not one of yours. The data doesn't say which are open.
  Status: done (2026-10-04: "NEAREST · CHECK YOUR LIST IN GAME" under an extract in the glance, with a tooltip and a
  sentence in help).
- **H4. Plan's other maps are below the fold**: the expanded card is about 840 px with six quests, so the folded
  cards to compare it with sit under it. Status: done (2026-10-04, approved by the owner from a before and after
  picture): the suggested maps are a short list on top (name and counts; the rows of the maps that aren't open keep
  the folded card's glyphs and bring cells), then the open map's card. The fake game has two maps; a look with
  four real ones is still due.
- **H5.** In a PMC raid the extract list is below the card. Status: done (2026-10-04): the list stays where it is, and the
  EXIT row carries "ALL 15 ↓", which brings it to the top of the rail. Another place for the list (above the
  quests, or folded) was not tried: say so if one click is one too many.
- **H7.** What needs a restart today: tiles after an offline start (A7), item sources after one failed download,
  another install's newer session (A20). Status: done (2026-10-04; all three recover by themselves now).
- **H9.** The first-run help can open during a raid; the crash question stays over the map until answered. Status:
  done (2026-10-04): the first-run help opens only outside a raid, a raid's start closes an open help panel, and
  the crash question waits while a raid loads or runs.

### C. Design elements

- **C1. The raid card's objective lines** are tarkov.dev's full sentences plus "Bring: MS2000 Marker" on each of three
  lines; Plan has the short synopsis. Status: done (2026-10-04, approved by the owner from a before and after
  picture): each line and NEXT say the objective by its synopsis phrase ("Mark Stryker"). The need stays on every
  line (owner: "keep the item with each sub-item so it's clear that one is needed"); saying it once under the
  quest's name was tried and declined.
- **C2.** A quest card says the map twice per objective ("… on Streets of Tarkov" over "Streets of Tarkov"). Status: done (2026-10-04; English texts).
- **C3. Plan card**: the rank numbers "1", "2" say what the order says; the dashed empty cell stands on most rows;
  the kit cue shows one item twice (to plant, to wear). Status: done (2026-10-04: all three gone).
- **C4. Status bar**: "NO POSITION YET · PRESS … IN RAID" also outside raids; three green lights with words for a
  state that is nearly always fine; below about 1,090 px the three buttons leave the window, which has no smallest
  size. Status: done for the two faults (2026-10-04): the position hint shows only in a raid, and a narrow bar
  drops the lights' words first (they stay in the tooltips). The three lights themselves are as they were.
- **C5. The boss symbol** is mostly its dark collar around a thin red ring, weaker than a quest's disc, and shares
  the diamond's outline with transits; boss labels are red where every other label is ink. Status: done
  (2026-10-04: a solid red octagon of its own, larger than before; its label is ink like every other, red only
  while pointed at).
- **C6.** Symbols at one place hide each other (an extract's triangle under a transit's diamond on Streets). Status:
  done for symbols of different rank (2026-10-04: the one that matters more lies on top; a Scav zone's ring lay
  over the boss's octagon and made it read as a ring). Two of the same rank at one place: done the same day, they stand side by side, each a few pixels beside its place.
- **C7.** The guide line's distance plate can stand on a symbol (it covered a boss marker in a snapshot). Status: done
  (2026-10-04).
- **C9. Help** is 2,350 px tall with 25 legend rows, and opens by itself at the first start. Status: done
  (2026-10-04, owner: "Rest I follow your suggestions"): help lists the symbols on the map shown, the others behind
  one link (`MapLegend.On`). In a Streets raid that is 18 rows and 2,280 px; a map with fewer kinds of things is
  shorter. The texts above the legend are as long as before.
- **C10. The quest types' icons** (owner, 2026-10-04: "the iconography of the quests can be improved. Find the best
  possible icons for each type used, compile a panel of different solutions and how they would look in the app to
  let me decide"). Status: done (2026-10-04, owner, from the panel: "B Solid Other Pictures but with the
  exploration icon from A Solid and with Survive icon from A solid"). The panel was made outside the repository:
  the glyphs until then (Windows' icon font and the app's own crosshair) and five sets from open icon families,
  each drawn in the real app (help's legend, Plan, the map) by a throwaway build. What it showed: thin outlines
  are faint in a map marker, where the glyph is about 10 px; filled shapes read at that size.
  - Now, all from Phosphor Icons (MIT): crosshair, magnifier, open hand, push pin, box, runner, handshake. Seven
    paths in `Glyphs`, drawn filled and fitted by their bounds on the map (`SKPath`) and in the lists
    (`KindGlyph`); the licence is in `THIRD-PARTY-NOTICES.md`, the credit in the README, the table in DESIGN.md §5.
  - Keys, the bring glyph, padlocks and switches still come from the icon font, so the README's Windows 10 note
    stays unless those are redrawn too.
  - The website's pictures and clip, and with them the README's picture, were re-recorded the same day (owner:
    "Make it so") and published. What had to follow in the site repository: Plan is rendered in a taller window
    (the map list pushed BRING out of a 900 px one), the quest card is shorter, the floor picker sits one button
    higher, and four callouts moved.

### D. Features (owner, 2026-10-04: these two; "the rest of d) not")

- **D1. Tick an objective done by hand.** The logs never say an objective is done, so a quest that takes several
  raids keeps leading to places already dealt with. The drawing is there (the done marker, the legend row). To
  build: a tick on the objective (quest card, outside a raid), kept per mode until the log reports the quest
  completed or failed, shown as done "ticked by you", left out of NEXT, the guide line and the planner's count.
  Status: done (2026-10-04: a tick box at the end of each objective's row on the quest card; DESIGN.md "Quest cards",
  "Ticks").
- **D2. Remember the window's monitor, size and whether it is maximised**; first start as today; a saved monitor
  that is gone falls back to today's rule. Status: done (2026-10-04; with a smallest size of 900 × 560).
- **D3. Delete position screenshots after reading** (owner, 2026-10-04: "a mode that auto-deletes screenshots after
  a couple of seconds grace period"). Status: done (2026-10-04): a tick in settings, off unless ticked; a screenshot
  that gave a position is deleted 5 seconds after its name was read, for good; what was in the folder before, menu
  screenshots and every other file stay (DESIGN.md §2, `ScreenshotCleaner`). For the owner to check: the 5 seconds;
  for good rather than to the Recycle Bin; menu screenshots staying; the setting's words. The website's "It never"
  list and a question of its own say it since 2026-10-04. Needs a run with
  the game: that the game's own write is over well within the 5 seconds (a file still open is tried again, so the
  worst case is a screenshot that stays).
- **Declined** (owner, 2026-10-04): adding or removing a quest by hand; opening with the game or with Windows; a
  text size setting. Don't propose them again.

### E. The linked highlight

- **E1. It stops at the quest.** An objective line in the raid card, NEXT and a quest card's objective light the
  whole quest, so the "69 m" line can't be told from the quest's other markers, though markers carry the
  objective's id. Status: done (2026-10-04: the objective's own places pulse, its line carries the tint, both ways).
- **E2. One tint for three things**: the row pointed at, the same thing elsewhere, and what is only related (an item
  row lights its quests and, through them, every other BRING row of those quests). Status: done (2026-10-04: the
  same thing keeps the full tint, what is related gets a weaker one, and an item no longer lights the other items
  of its quests; `LinkStrength`).
- **E3. Places that link nothing**: need cells, folded cards' glyphs, boss names in the raid line and on Plan cards,
  the gold "Key: …" line under an objective, an extract's need item, the item card's "Loose on …" row; entering an
  item card drops the item's locks and loose spots; a quest card's frame and headings let the quest go while it is
  read. Status: done for need cells, the "Key: …" line (when it names one key) and folded cards' glyphs (2026-10-04), and
  since then for a card's whole body, the item card's "Loose on …" row and an extract's need item. Boss names in the raid line and on Plan cards: done the same day (each links to its spawn zones on the map shown).
- **E4.** An "A or B" row links only A (`Planning`: `Alternatives[0]`). **E5.** A quest lights its doors, a door only
  its key. **E6.** The highlight needs a pointer: rows can't be reached by keyboard. Status: done (2026-10-04; E6: Down and Up step through the rail's rows, Enter is the click, P the pen).

### F. Floor plans for the maps without SVG artwork

- **F1. The Lab, Labyrinth and Icebreaker in the style of the SVG maps** (owner, 2026-10-04: "convert it to the
  same style and color coding as the ones where we have proper maps … only do this if we are sure that we can
  achieve high quality"). Status: on hold (owner, 2026-10-04: "Write this up and put in on hold for now"); don't
  take it up or propose it until the owner says so. A trial the same day traced a floor plan out of
  the cached tile renders (`tools\map-trace`): to scale and with straight walls on The Lab's main level (the plan
  differs from the render's floor by 0.003 % beyond 2 px of an edge), ragged on Labyrinth. That settles the
  geometry, not the look: a hand-drawn map chooses obstacles, stairs and landmarks, and the traced plan has
  floor and not-floor only. `docs/MAP-TRACE.md` has what was measured, the steps, the gates a map must pass and
  what the owner decides; it is written so that a map keeps its render unless every gate passes. Icebreaker is
  left out. Pictures of the trial are outside the repository. Looked up the same day: re3mr has hand-drawn plans
  of Labyrinth and Icebreaker under CC BY-NC-SA 4.0, which could be traced in place of the renders (the file's
  "Other maps that exist"); whether they are to scale is not measured, and using them is the owner's decision.
  The file's "Where this stands" has what is known, what isn't, and the order to take it up in.

## Queued for Monday, 2026-10-05 (owner, 2026-10-03: record now, implement when there's quota again)

**Status, 2026-10-03 evening:** the quota came back the same day and the owner said "go ahead with the open TODOs".
Done and merged (DESIGN.md has each decision): 1, 2, 3, 5, 6, 7, 8, 9, 11, 12, 13, 14 (with the changelog the
owner added), 15, 16, and 17 (the design system, §4). Still open:
- **4, click checks:** in the installed dev build ("Shturmap DEV", F12 for the developer view), with the owner.
- **18:** item 2 below (local vs server raids), and the release checklist (on hold until the release).
- Version: still 0.2.0, though much has changed since the build sent to a friend; ask before the next one goes out.

**Later the same evening, also done:** 10 (the FAQ, heading "Is this allowed? Can I get banned?", published with
the website update on 2026-10-03: new media from the newest app, release wording, design-system CSS; the README's
"Before you install"); tarkov.dev's tile renders for The Lab, Labyrinth and Icebreaker; landmarks (locked doors and
keys, switches, Labyrinth's traps); minefields and border-sniper zones, clipped to the drawn map; AI squads (Rogues,
Raiders, cultists…) as spawn markers; entry items in BRING; extract–switch links (none link with today's data);
"Uninstall Shturmap…" with optional data deletion; feedback · help · settings as three buttons; the no-game
fallback (item 6 below); the kit reminder while a raid loads.

The items below are kept as written, for their reasons and quotes.

1. **A separate data folder for dev builds** (proposed, waiting for the owner's go). Today the installed release
   (`%LOCALAPPDATA%\ShturmapApp`) and the folder build (`artifacts\Shturmap`) share `%LOCALAPPDATA%\Shturmap`: one
   database and settings, one app log (diagnostics mix both), one study log (dev starts pollute the owner's data),
   one outbox; folder builds have the DSN, so their crashes reach Sentry too. Proposal: installed copies keep
   `%LOCALAPPDATA%\Shturmap`, every other build uses `%LOCALAPPDATA%\Shturmap-dev` (`AppPaths.CreateDefault`), the
   download cache stays shared (`CacheRoot`), plus a `--data <folder>` switch. Quest states rebuild from the game logs;
   only manual quest states stay with the release. About 20 minutes with tests. The owner then installs the 0.2.0
   Setup beside the dev build.
2. **One symbol, one meaning: the flea market's shopping bag** (owner to decide). `E719` is both the Find-in-raid
   quest type and the flea-market source on item cards (found by the symbol check, DESIGN.md "one symbol, one
   meaning"). Suggested: a price tag for the flea market.
3. **Old release files** in `artifacts`: `Shturmap-0.1.0-win-x64.exe`/`.zip` (+ `.sha256`) are superseded by
   `Shturmap-Setup.exe` 0.2.0; delete them on the owner's OK so there's one file to send.
4. **Not yet verified by a click** (snapshots and tests only): the pop-out button, RESTART NOW / DOWNLOAD and the
   Updates setting, the study-log switch (dev build only since 2026-10-03), Copy diagnostics, LOG FOLDER, LICENCES, PRIVACY, the report dialog's Send,
   the crash question's buttons, the effort hairlines and new labels at 125 % and 150 % display scaling. Ask the owner
   to try them in the installed 0.2.0, or walk through them together.
5. **PROGRESS rows look less important than they are** (owner, 2026-10-03: "the quests under progress have a
   different styling, which in some way makes sense since they are not to be completed during this raid. However,
   there is a bit of a weird cognitive mismatch since this encoding suggests less importance. This should be fixed.")
   Today the Plan card draws PROGRESS rows muted (name, glyph and trader portrait dimmed), which reads as "disabled"
   or "unimportant", while these quests are as much the player's work this raid as COMPLETE ones. Direction to
   propose before changing anything: same ink and strength as COMPLETE rows; the section heading alone says
   "progress", and if a row needs more, a factual note instead of dimming (e.g. "2 of 5 objectives here", or why it
   can't finish: "needs found-in-raid items", "kills over several raids"), taken from what the planner already knows.
   Check the raid card's rows and the folded cards' glyphs (gold to complete, muted to progress) for the same issue,
   and keep "muted = after the raid / at a trader" (objective rows) as the one meaning of muted. DESIGN.md decision.
6. **BRING should also cover weapons and gear that kill objectives require** (owner, 2026-10-03: "There are others
   that still require items, such as doing kills with certain weapons or weapon classes. This is not reflected in
   the app, but should still be there. It should be a coherent design so it is still clear what to bring and what is
   needed to solve a quest. Find the best solution here.") To work out on Monday, then propose before building.
   Starting points:
   - **Data** (tarkov.dev `shoot` objectives, PvE, counted 2026-10-03): `usingWeapon` on 64, `usingWeaponMods` on 9,
     `wearing` on 13, `notWearing` on 5 (item lists, some long: a "class" like bolt-action rifles arrives as every
     member's id). Check whether tarkov.dev gives a category to name a class by ("any bolt-action rifle, 18 kinds"),
     and how other objective types carry gear (e.g. `buildWeapon`, `useItem`).
   - **Today's BRING** lists keys ("key for …"), items to plant or mark ("to plant, for Dandies", "to mark, for
     Revision"), found-in-raid items, with icon, source (trader level, flea price) and loose spots. A coherent
     extension keeps that row shape and adds the verb: "to use, for Wet Job" (one of several weapons, or a class),
     "to fit, for Job for a Patriot" (mods), "to wear, for Dandies" (gear). "Don't wear" (`notWearing`: no armor, no
     helmet) isn't an item to bring: a short note on the quest instead, not a BRING row.
   - **Extracts that need an item** (owner, 2026-10-03: "For example in the cease fire quest it is not clear that we
     need to bring a flare from the app"). Cease Fire! asks to extract through Klimov Street (Flare); the extract
     list already says what each exit takes (`ExtractRules`: flare, paracord and ice pick, money, …), but the
     quest's BRING doesn't. An `extract` objective that names an exit (internal key, `GameData.ExtractKeys`) whose
     requirement is an item gets a BRING row: "to leave through Klimov Street, for Cease Fire!" (the flare, with
     icon and source). The same for `useItem` objectives that use an item (e.g. Airmail's red signal flare), if
     BRING doesn't list them yet. Check which quests this touches with an audit over the data.
   - **Rules that hold:** only what the data says (Shturmap knows no inventory, so never "you have it"); one row per
     item or class, listing the quests it serves; the same in Plan's BRING, the raid card's BRING and the quest card;
     the synopsis line already names the weapon in words, BRING adds icon, source and price. Check the effort
     grouping (kill conditions) and the synopsis stay consistent with it.
7. **"~17 min walking" on the map cards** (owner, 2026-10-03: "probably misleading since it is not clear on which
   quests this is related to, also it's also not realistic since you rarely constantly walk in the game. Either get
   rid of this or find a better solution.") It comes from `RaidPlanner`'s route length over the places of the
   quests it lists, at a walking pace. It is also an estimate shown as a figure, which the owner's truthfulness rule
   (only what was read; no estimates in the UI) already argues against. Recommendation to bring on Monday: remove
   it, from the cards, the cue and anywhere else it appears (and its DESIGN.md mentions). If the owner wants
   something in its place, only a fact with a clear subject, e.g. "objectives in 3 places" or the straight distance
   between the two farthest places of the listed quests, and never a time.
8. **"IN THE MENUS" while the game isn't even running** (owner, 2026-10-03: "This is misleading. Find a better
   wording that is coherent.") The status bar shows the raid phase `Menu` by default, so a closed game reads as "in
   the menus". To work out on Monday, from the logs only: no process checks, since the boundary (DESIGN.md §2) rules
   out touching the game's process, and listing processes is close enough to keep out. Check the owner's
   application logs for a line the game writes when it quits, and whether a session's log simply stops. Likely
   shape: the status says only what the log shows. "GAME CLOSED" after a logged quit; "IN THE MENUS" only while
   the current log session is live and in the menus; otherwise the plain "NOT IN A RAID", which is always true.
   Keep the words coherent with the other states (raid loading, in raid, raid over) and the tooltips, and update
   help and DESIGN.md.
9. **The status lights and words at the top right don't line up with the "?" button** (owner, 2026-10-03). After the
   lights were raised 2 px onto the middle of their capitals (`baeb938`, Sentry SHTURMAN-2), the row of lights and
   capitals and the 28 px help button are still centred differently: the words' text box versus the button's box,
   and the "?" glyph inside it. Fix so the capitals, the lights, the "?" and the button's frame share one middle line
   (the left side too: logo, mode label, raid state). Check with an enlarged snapshot crop, as for SHTURMAN-2, and at
   125 % and 150 % scaling.
10. **An FAQ at the end of the website, for new and experienced players, and maybe in the README** (owner,
    2026-10-03: "At the end of the website, put a FAQ that related to new and expert gamers and go into the 'is this
    cheating' thing based on our research. Maybe also add this to the GH readme if it makes sense.") This is the
    owner's go for a website change; do it in one pass with the release's website items ("At the production release",
    item 5: risk line, article 437, PvP sentence; the "nothing is sent" wording; media refresh), and draft before
    publishing.
    - **New players:** what it is and what it isn't (a quest planner and a map of where your last screenshot was
      taken); how the position works (press your screenshot key); PvE and PvP; which
      maps; game languages; what it costs (nothing) and what it sends (only reports you send, the update check).
      "Do I need to know the maps?" was in the first version and came out again (owner, 2026-10-04: "Remove the
      'Do I need to know the maps?' question from the FAQ"); don't put it back.
    - **Experienced players, "is this cheating?":** answered from the research (DESIGN.md §2): what it reads and
      never does; BSG's support article 437 describes the coordinates in screenshot names (a source, not an
      endorsement); the License Agreement 4.3.4 and no guarantee against sanctions; no BSG statement on such tools
      and no verified ban found, as of 2026-10-03; the PvP debate; never shares a position, never takes a
      screenshot for you; open source, so anyone can check.
    - **Open for the owner:** earlier the owner wanted the site to avoid the word "cheating" and instead say what it
      relies on and never does (2026-10-02). An FAQ answers players' own question, so propose the question's
      wording (e.g. "Is this allowed? Can I get banned?" versus "Is this cheating?") before writing it.
    - **README:** if it makes sense, the three or four questions that matter before installing (allowed / banned /
      PvP / what it sends), linking to the website's full FAQ; no duplication beyond that.
    - Follow DESIGN.md §2's wording rule (no cheat-seller vocabulary, no hint of approval).
11. **No big "POSITION 7 MIN OLD" over the map; the age goes beside the marker** (owner, 2026-10-03: "Get rid of the
    'Position X minutes old' big title feedback in the app. Put it next to the marker"). Today, in a raid, a position
    older than `StaleAfter` (MainWindow.xaml.cs, about line 375) shows in big type over the map (MainWindow.xaml,
    about line 717; an earlier decision from the study log: positions came about every 8 minutes and were often
    minutes old when the app was looked at). The marker already carries a dashed ring and an age tag ("4 MIN") once
    a position is a minute old (cartography change 5), and the guide line's plate says "69 m · 4 MIN". So: remove the
    big type; check the marker's age tag reads well enough at a glance on its own (size, contrast, the collar), maybe
    "7 MIN OLD" once past `StaleAfter`; keep the raid card's note ("Distances from your screenshot 7 min ago").
    DESIGN.md: record that the owner replaced the earlier decision, with the date.
12. **Other markers fade too far while a quest is highlighted** (owner, 2026-10-03: "the other markers are rendered
    too faintly, they can be barely made out anymore, but are still pretty important"). `MapRenderer.Render` draws
    everything not in focus in one layer at `1 - 0.72 × Dim`, so at full dim other markers keep 28 %, labels
    included. Rework so the focused quest still stands out, but the rest stays readable:
    - fade much less, around 55–65 %, and check it on the real artwork, which is already receded;
    - or step back by colour, not opacity: desaturate other quests' markers and keep their shape and collar crisp;
    - never fade what's always needed: extracts and transits for your side, the player, boss and sniper markers
      maybe less than quest markers;
    - fade the labels of the others more than their symbols.
    Compare candidates side by side in snapshots (a highlighted quest on Customs and Streets) before choosing, and
    record the decision in DESIGN.md (the four-level rule in "Map drawing"). **The raid view matters most** (owner:
    "This is probably especially relevant in the raid view"): there a quest is often kept highlighted for a long
    time, while extracts, other objectives nearby and bosses still matter at a glance. So judge the candidates in a
    fake-raid snapshot with a kept quest (`tools\fake-raid.ps1 -ShowQuest …`), and consider fading less, or not at
    all, in a raid than in Plan.
13. **Mark optional objectives on the map** (owner, 2026-10-03: "In the quest markers, encode somehow when a marker is
    optional. It might still be very relevant for a quest."). tarkov.dev flags objectives as `optional`; the planner
    already leaves them out of "can be completed" (`RaidPlanner`), but the map draws their markers like required
    ones. To do on Monday:
    - Count how many optional objectives have places on the map, per map, and which quests they belong to.
    - Find an encoding that respects "one symbol, one meaning" (DESIGN.md). Already taken: the hollow gold ring
      (one of the possible places), muted grey (done or after the raid), the dashed ring (an old position), and the
      badge corners (floor at the upper right, cluster count at the lower right). So a new, distinct mark is needed,
      e.g. a small "OPT" tag beside the label, or a lighter collar style. Mock up two or three and compare them in
      snapshots.
    - Say the same in words where objectives are listed (quest card, raid card): "optional".
    - Add a legend row, and record the decision in DESIGN.md.
14. **A dev view to play a fake raid from inside the app** (owner, 2026-10-03: "Implement a Dev view that allows to
    switch between modes and can fake-play a raid that reacts to what I do in the app and 'fake-updates' the
    position on demand. This one should not be exposed in the production version, only in the dev version. I want
    to use this to check the UI without having to play the game.") Builds on what exists: `--fake-game <folder>`
    (the app reads a fake game folder's Logs and Screenshots), `tools\fake-raid.ps1` (scripted lines),
    `shturmap-cli simulate`, `Demo.cs`. Plan to propose on Monday:
    - **A side panel, only in dev builds,** compiled out of releases (a build property or `DEBUG`, not just
      hidden). A test checks that the release assembly has none of it. It runs against its own fake game folder
      and the dev data folder (item 1), never the real game or the owner's data.
    - **It writes real log lines and screenshot names** into the fake folder, so the whole pipeline is exercised
      as in a game:
      - mode: PvE, PvP, Seasonal (the `Session mode` line, now that the app has no chooser);
      - map; group pick; loading steps; raid start (PMC or Scav, local or server); raid end;
      - quests started, objectives done, quests completed or failed (push notifications);
      - transit.
    - **The position on demand:** click a spot on the map in the dev view (or press a key) and it writes a
      screenshot name with that position, the facing (drag for the direction) and the floor height, as the game
      would. Also "age the position" to check the old-position look, and walk a short path.
    - **Also useful:** trigger the report dialog, a crash question, the update-ready line, a data-load failure
      (offline, 404, 503), and a missing game.
    - Keep it plain and developer-styled. It doesn't follow the product's visual rules, but must never leak into a
      release; DESIGN.md §8 "Developer aids" describes it.
15. **A dev build next to the release, always current, with its own icon** (owner, 2026-10-03: "I want to have a dev
    artifact of the app that always carries the recent updates and is in dev mode. The artifacts should contain a
    dev and release artifact. The dev version should carry a different icon so it is visually clear.") Plan to
    propose on Monday, together with items 1 (dev data folder) and 14 (dev view):
    - **`artifacts\` holds two things:** `artifacts\release\` (the Velopack Setup and packages, as
      `eng\release.ps1` makes today) and `artifacts\dev\` (the dev build). The old 0.1.0 files go (item 3).
    - **The dev build is in dev mode:**
      - the dev view (item 14) and the `Shturmap-dev` data folder (item 1);
      - no reports to Sentry, or reports marked as environment "dev";
      - no update check against GitHub;
      - a window title "Shturmap DEV" and its own app id, Start-menu entry and shortcut names, so it never mixes
        with the installed release.
    - **"Always the recent updates":** either (a) Claude rebuilds `artifacts\dev` after every change it commits (a
      CLAUDE.md rule, with `eng\dev.ps1`), or (b) the dev build is itself a Velopack install on a "dev" channel that
      updates from a local feed in `artifacts\dev\feed`, so the installed dev app picks up each new build at its
      next start. (b) is closer to "always current" without thinking about it; propose both.
    - **Its own icon:** a dev variant made by `brand\build.cs` (exe, window and taskbar icon). Keep the mark and
      change only something unmistakable but safe, e.g. the plate in the app's cyan, or a small "DEV" band. The
      owner checks logo changes against symbol resemblances (DESIGN.md §4, "Logo"), so show a panel of two or
      three variants first.
16. **Pick several quests for the coming raid** (owner, 2026-10-03: "I like how easy to use the app is right now
    and how little interaction you have to make with it in order to get a benefit. I feel we're missing one thing
    though. When preparing a raid, you typically bring items for specific quest or want to run a specific quest.
    Therefore you likely want to have something like the current 'mark quest' thing, but for all the quest you want
    to tackle in the map. Still it should show all other quest markers. It should incorporate hazzle-free and well
    designed and fitting into the existing app. Choose the best design possible.") Work out the design on Monday
    and show it as mocks before building. Starting points:
    - **Today:** the highlighter keeps one quest lit: cyan markers and a guide line to its nearest place. Picking
      several would grow out of that same control: the pen on a Plan row toggles the quest into "this raid's
      picks", so it costs one click per quest while planning, and none in the raid (DESIGN.md: no clicks in a raid).
    - **On the map:** picks in the kept look (cyan, ringed); every other quest marker stays at normal strength, not
      faded (ties in with item 12); the guide line goes to the nearest place among all picks.
    - **In the rail:** the map's card shows the picks first, as their own small group or with a mark; BRING puts
      what the picks need first ("for your picks"); the raid card then starts from the picks, nearest first.
    - **Lifetime, without upkeep:** picks hold from Plan through loading into the raid; a quest the log reports
      completed leaves the picks by itself; picks for another map wait for that map; decide whether picks clear at
      raid end or stay until done. One quiet "Clear picks" outside raids.
    - **Zero picks = today's behaviour,** so nobody has to use it. Check it against the effort order (item list
      order), the synopsis lines and the folded cards; one symbol, one meaning (the pen keeps its meaning: "keep
      lit", now for several). DESIGN.md decision, help text, legend.
17. **Done (2026-10-03): item 5 below, one design system** (owner: "Yes, do the design system after this round"): DESIGN.md §4 "Design system", `Shturmap.Map.Palette`, `DesignTokenTests`; the website's CSS follows it on the unpublished FAQ branch.
18. Still open from before: items 2, 5 and 6 below, and the release checklist ("At the production release on GitHub"),
   which includes the optional repo-scan test (token shapes, DSN, claude.ai links, user-folder paths).

## Before starting

- **Releases** (2026-10-03): 0.1.0 went to friends by hand as one exe. From **0.2.0**, the first public release,
  Velopack: `eng\release.ps1` builds the Setup and update packages into `artifacts\releases` (plus
  `artifacts\Shturmap-Setup.exe`), `eng\publish-release.ps1` publishes them as a GitHub pre-release, and the
  installed app keeps itself up to date (DESIGN.md §8, "Distribution"). `eng\release.ps1` refuses while Shturmap
  runs from `artifacts\Shturmap`; `eng\publish.ps1` alone builds only that folder and doesn't empty it first.
- **Boundary (owner's rule).** Read only `*application_*.log` and `*push-notifications_*.log` in an EFT log
  session folder, never `*.log`: that glob also matches `backend_000.log`, which is off limits. No memory, no
  input, no screen capture.
- **Verify without the owner's screen**: `Shturmap.exe --snapshot <folder> [seconds] [--show-quest <name>]`,
  `tools\fake-raid.ps1` (`-Scav`, `-LocalRaid`, `-PlanOnly`, `-ShowQuest`, `-SnapshotAfter`), and
  `shturmap-cli render` for map drawing. The fake raid writes application and push-notification lines; extend
  it with writers for the new lines below.
- Parsing lives in `src/Shturmap.Core/Logs/GameLogParser.cs` (application lines, and `ParseNotification` for
  push lines; `UserConfirmed` is already read as a `MatchSetupEvent`), events in `GameEvents.cs`, raid state in
  `src/Shturmap.Core/Raid/RaidTracker.cs`, the session in `src/Shturmap.Session/GameSession.cs` (`Apply`,
  `Announce` for big cues, `Say` for notices, `StudyRaid` for the study log), the UI in
  `src/Shturmap.App/MainWindow.xaml(.cs)`. Tests for parsing and the tracker go in
  `tests/Shturmap.Core.Tests/LogTests.cs`.

## 1. Group raids: show the leader's map before loading

**Done (2026-10-02).** In the owner's logs the pick came 20–70 s before loading (5 picks, 2 sessions, each followed
by that map loading). Day/night is left out: the Tarkov clock formula matches 12 of 13 screenshot raid clocks, but
no screenshot exists from a raid whose time variant is known, so which time "CURR" and "PAST" mean is unchecked;
the study log now records the variant (`group.pick`) and the raid clock (`fix`) to settle it. `fake-raid.ps1
-PlanOnly -GroupPick` shows it.

**Why.** In a group, the leader picks the map in the menus; Shturmap only learns it when loading starts, too late
to change gear. The push log says it earlier.

**Evidence** (push-notifications log, several times in one session):

```
Got notification | GroupMatchRaidSettings { "type": "groupMatchRaidSettings", "eventId": "…",
  "raidSettings": { "location": "Sandbox_high", "timeVariant": "PAST", "raidMode": "Online",
  "metabolismDisabled": false, "playersSpawnPlace": "SamePlace",
  "timeAndWeatherSettings": { "isRandomTime": false, "isRandomWeather": false, "cloudinessType": "Clear", … } } }
```

Related kinds seen: `GroupMatchInviteSend` (2), `GroupMatchRaidReady` (5), `GroupMatchRaidNotReady` (12),
`GroupMatchStartGame` (5), `UserMatchCreated` (10). The application log has `Matching with group id: <n>` at
matching (the number is empty when solo).

**Plan.**
- Core: `GroupRaidSettingsEvent(At, LocationId, TimeVariant)` from `ParseNotification`; optionally
  `GroupStatusEvent` for ready / not ready / start. Location ids are the same kind as in the match setup
  ("Sandbox_high", "bigmap", "TarkovStreets"): resolve with `data.CreateResolver().Resolve(null, locationId)`.
- Session: on a live (not replayed) group pick while in the menus, show that map (as `SelectMapAsync` does), say
  "Your group picked Ground Zero · bring: …" with the same bring list as the loading notice, and `Announce` a new
  `CueKind.GroupPick` (eyebrow "GROUP PICKED", the map, the plan's summary). A later pick replaces it; loading
  follows as today.
- `timeVariant` is "CURR" or "PAST": one of the two raid times 12 h apart. Turning it into "day" or "night" needs
  the in-game clock (Tarkov time runs at 7× real time). Work out the formula, check it against the raid clock the
  screenshots give (`RaidClockHours`), and only then show "night raid". Until then, leave it out.
- Study log: `group.pick` (map, time variant), group ready / not ready / start.
- Tests: parser test with the sample above (ids masked).

## 2. Say whether a raid runs on the player's machine

**Why.** The PMC ⇄ SCAV switch is only needed for raids hosted on the player's machine. PvE uses both kinds:
server-hosted on 25, 26 and 30 September, local for two raids on 26 September and all four on 1 October.

**Evidence** (application log).
- Server-hosted: during loading `TRACE-NetworkGameCreate profileStatus: 'Profileid: <id>, Status: Busy,
  RaidMode: Online, Ip: …'`. Right after `GameStarted` comes ``[Transit] `<profileId>` Count:0,
  EventPlayer:False``, naming the profile that plays; in PvP the Scav profile's id is the PMC's plus one.
  Durations are non-zero, e.g. `GameStarted:90.6(10.33)`.
- Local: no `NetworkGameCreate`. `GameStarting:55.73(0) … GameStarted:55.73(0)` (zero in brackets), then
  `[Transit] Flag:Common, RaidId:<id>, Count:0, Locations:bigmap -> `. A cancelled match says
  `Local game matching cancelled.` instead of `Network game matching cancelled.`

**Plan.**
- Core: parse the bracketed duration of `GameStarting`/`GameStarted`, and the two cancel lines.
  `RaidState.Hosting` is `Unknown | Server | Local`: Server on a `MatchSetupEvent`; Local on a zero-length start
  with no setup, or "Local game matching cancelled.".
- Side: unchanged for server raids (the log tells). For local raids keep the switch, and say why in its tooltip
  and in `sideEvidence` (already logged as `setup:none,starting:yes`); add `hosting` to `raid.start`.
- Tests: tracker replays with both shapes (fixtures from the lines above).

## 3. Loading progress in the RAID LOADING cue

**Done (2026-10-02)**, as a line in the raid card naming the last step the log reported, over one segment per step
that lights only when the log reports it; nothing estimated (owner). The cue stays 5 s, with a slower entrance. In
49 loads: location loaded at 25 s, spawned 42 s, pooled 47 s, raid start 71 s (31–139 s). Each raid's step timings
go to the study log in `raid.start`. `fake-raid.ps1 -HoldLoading` shows it.

**Why.** Loading takes 60–130 s and Shturmap shows nothing between the 5 s cue and the raid start.

**Evidence** (application log, a local Customs raid, about 72 s in all):

```
13:00:00.000  MatchingCompleted:0 real:0 diff:0
13:00:01.500  scene preset path:maps/customs_preset.bundle …
21:01:13–15   TRACE-NetworkGameMatching G / H / I
13:00:30.000  LocationLoaded:23.5 real:29.87
13:00:31.000  GamePrepared
13:00:32.000  GameCreated
13:00:32.300  [Transit] Flag:Common, RaidId:…, Count:0, Locations:bigmap ->
13:00:38.000  PlayerSpawnEvent
13:00:55.000  GamePooled
13:01:13.000  GameRunned / GameSpawn / GameSpawned / GameStarting / GameStarted
```

**Plan.**
- Core: a `LoadingStepEvent(At, Step)` for LocationLoaded, GamePrepared, GameCreated, PlayerSpawnEvent,
  GamePooled; `RaidState.LoadingStep`.
- UI: after the cue, one quiet line in the raid card until the raid starts: "LOADING · SPAWNING", with a thin
  rule that fills by step. Optionally a rough "about 40 s" from typical step offsets (median of past raids in the
  study log, or fixed numbers from the sample). Keep it to one line (minimal-UI rule).
- Study log: per-raid step timings, matching time.

## 4. A hint of how the raid ended (study log only)

**Done (2026-10-02).** The logs held two insurer notes (type 2, Prapor, with the raid's location) and two returns
(type 8). Both notes came 17–20 s *before* the raid's end line, not after it, so a note counts during the raid and
up to 5 minutes after it (`RaidOutcomeHints`); the Streets fixture gives one hint. Never shown in the UI.

**Why.** The study log can't tell a survived raid from a death, so plan accuracy can't be judged. Nothing in the
allowed logs says it directly, but the insurer writes when insured gear was lost.

**Evidence** (push-notifications log).
- A trader message, `message.type` 2, arriving right after a raid, with
  `"systemData": { "date": "<dd.MM.yyyy>", "time": "<HH:mm>", "location": "bigmap" }`, `"templateId": "<id> 0"` and
  `"hasRewards": false`. This is most likely the insurer's "items lost" note (seen twice).
- `message.type` 8 (insurance return) comes later with `"items"` and the same kind of `systemData`.
- Message type numbers to verify against what is seen: 2 trader, 8 insurance return, 10 quest started, 11 quest
  failed, 12 quest completed (10–12 are already parsed as quest events).

**Plan.**
- Core: `InsuranceNoticeEvent(At, Kind: Lost | Returned, LocationId, ItemCount)` from types 2 and 8 when
  `systemData.location` is present.
- Session: a "lost" note within about 5 minutes after a raid ended on the same location gives the study log a
  `raid.outcomeHint` event `{ lostInsured: true }`. Never show it as a fact: no note proves nothing, since gear may
  not have been insured.
- With item 1's quest ids at raid load (already logged as `complete` / `progress`), this lets a later study
  compare plans with what happened.

## 5. One design system for the app, the README and the website

**Done (2026-10-03):** DESIGN.md §4 "Design system" (colour tokens with roles, type roles for app and web, shape and
spacing, motion, icons, logo use, words, and where each surface takes them from); `Shturmap.Map.Palette` as the
colour table in code; `DesignTokenTests` keep App.xaml, XAML colours, the map, the brand files, the design doc and
the website's CSS equal to it.

**Why** (owner, 2026-10-02). With the logo settled (DESIGN.md §4, "Logo"), the colours, type and general look must
be the same in the app, the README and the coming website, and DESIGN.md has to say how.

**Plan.** Write one design system into DESIGN.md and align `App.xaml` and the README to it:
- colours: the palette, plus the light-background variants that so far exist only for the logo;
- type: Bahnschrift in the app; a DIN-like font with a licence that allows web use for the website, since
  Bahnschrift may be used on screen but not served as a web font (Microsoft's font FAQ); how the README fits in;
- shape, spacing and motion rules, the logo's use (clear space, minimum sizes), and the tone of the text.

## 6. A useful default mode without the game installed

**Why** (owner, 2026-10-03, "for later"). Someone without Escape from Tarkov on this PC still opens Shturmap: to
look at it before installing, or on a laptop beside the PC that runs the game. Today they get one 30 s notice
("Couldn't find Escape from Tarkov on this PC, so quests and raids won't follow the game…", `GameSession.
ReportGameFolders`) and then an app built around quests it can't know.

**What still works without the game:** tarkov.dev's data (all maps with artwork, extracts and transits with their
requirements, spawn zones, bosses with chances, map names, loose loot spots) and the help. What doesn't: active
quests, raid state, positions (all from the game's logs and screenshot names).

**Done (2026-10-03, owner: "Yes, build the fallback 1-5"; DESIGN.md "Screen anatomy", *No game*):**
1. One line where the Plan card would be, for as long as no game is found: "NO GAME FOUND ON THIS PC" ·
   **CHOOSE GAME FOLDER…** · "or browse the maps"; a variant when the game is found but hasn't run. No 30 s notice.
2. **Choose game folder…**: Windows' folder picker, checked with discovery's rules, saved, and followed at once
   without a restart (logs re-pointed and read for history).
3. Browse mode: the map as always, a PvE / PvP / Seasonal chooser in place of the mode label, nothing quest-like.
4. Discovery runs again every 30 s while the game or its logs are missing, and switches over by itself.
5. Developer switch `--no-game` and the developer view's "No game" trigger, plus its script step `choose`.

**Still open:**
- The laptop case: reading the game PC's logs over a network folder. A network folder chosen with **Choose game
  folder…** that holds the game already works; whether to say so (README, FAQ) and support it is the owner's call.
  Screenshots stay the local Documents folder's, so positions wouldn't arrive over the network.
- A quest lookup without the game (every quest on a map, labelled as all quests, not "yours") was not built; the
  browse mode shows nothing quest-like.
- Done since: settings have the "GAME FOLDER: <path or 'not found'>" row with CHOOSE… and FIND AUTOMATICALLY, so
  a found-but-wrong install can be changed too.

## At the production release on GitHub (owner, 2026-10-03)

Raise these with the owner when the app gets its first public GitHub Release (not for builds passed to friends):

1. **Website media.** Re-record the screenshots and the hero clip (`tools\make-media.ps1` in the site repository),
   only when the owner says so.
2. **Reporting is one route: in the app, to Sentry** (owner, 2026-10-03: "don't want to mix github issues and other
   reporting methods, it should be one coherent easy to use thing"; done, DESIGN.md §8, "Reports"). This replaces
   the earlier plan of GitHub issue forms plus a separate web form. Before the release:
   - the release is built with the DSN (`eng\sentry.dsn`, untracked; `eng\release.ps1` warns without it), and
     `Shturmap.exe --send-report "<text>" <folder>` from the release exe opens the Report dialog with the text;
     after a click on Send it says "Sent. Thank you.";
   - in the Sentry project, IP addresses aren't stored (Settings → Security & Privacy → "Prevent Storing of IP
     Addresses") and the data scrubbers are on;
   - the README and the website point to "Help (?) → Report a problem or idea" (the website only on the owner's
     word), and the website's "sends nothing" wording becomes "Nothing is sent unless you send a report or allow
     crash reports";
   - `PRIVACY.md`: the owner fills in the controller and contact (and the retention of the Sentry plan), and has it
     checked.
3. **Delivery: GitHub Releases with Velopack** (owner, 2026-10-03: releases in the code repository, an auto-update
   mode, and "we can move for the first public release, but we should state that the app is still in private
   testing"). Done: the Setup, updates (Automatic / Tell me only / Off, never during a raid), `eng\release.ps1` and
   `eng\publish-release.ps1`, the README's private-testing note, `docs\release-notes\0.2.0.md`, version 0.2.0; the
   whole path (install, update to a newer version from a local feed, uninstall with the data folder untouched) was
   tested on this PC on 2026-10-03. The owner's steps:
   - make `github.com/shturmap/shturmap` public (the app's update check asks GitHub anonymously, which only works
     for a public repository);
   - commit and push, then `.\eng\release.ps1` and `.\eng\publish-release.ps1` (or `-Draft` to look at it on GitHub
     before publishing). It uses `GITHUB_TOKEN`, `gh`'s login or Git's stored GitHub login;
   - the first real update check against GitHub happens with the second release (0.2.1): install 0.2.0 from the
     release, publish 0.2.1, and see that 0.2.0 offers it. Until then the GitHub path is untested; the local feed
     path is;
   - friends with the 0.1.0 exe run the Setup once (the release notes say so).
4. **Repository settings and who publishes** (on hold until the release, owner, 2026-10-03). Before the repository
   goes public: turn off GitHub Issues and Discussions (reports go through the app), decide whether releases are
   published by the owner's account or by GitHub Actions, and whether the maintainer's identity is shown on the
   repository's pages.
5. **Wording and risk, from the web research of 2026-10-03** (owner: "put points 1–5 on the release checklist";
   DESIGN.md §2 has the findings and rules). Done in the README and DESIGN.md on 2026-10-03; still to do on the
   website (only on the owner's word) and in the release notes (`docs\release-notes\0.2.0.md`):
   1. the precise risk line: not made or endorsed by Battlestate; its License Agreement (4.3.4) lets it decide which
      outside software is allowed; no guarantee against sanctions; use at your own risk;
   2. where the coordinates come from: Battlestate's support article 437 (reporting a bug) describes them in the
      screenshot name; a source, not an endorsement;
   3. one honest sentence on PvP: some players see a map with your own position as an unfair advantage in PvP; most
      useful for learning maps and in PvE; and the two rules (never shares a position, never takes a screenshot for
      the player);
   4. done: the two lasting rules and the wording rule in DESIGN.md §2;
   5. a change by Battlestate: if the screenshot names lose their coordinates, or its planned "simplified GPS"
      (roadmap, December 2026, reported only second-hand) changes things, Shturmap must keep working as a quest
      planner without a position. Before the release, check what a screenshot name without coordinates does (a
      fake-raid run with such a name), and say it in the README and on the website.

## Not asked for (owner to decide)

- Lines like `Reason:Lift, Position:(x, y, z), SpeedLimit:…, CurrentState:Run…` carry a world position (after an
  elevator ride, a speed check, a network hiccup). There were 14 in 20 sessions, which is too rare to rely on.
  They look like the game's movement-check diagnostics: ask the owner before using them.
- History since August (maps played, raid lengths, quest starts and completions) could ground Plan in the
  owner's own record.
- Low value: quest reward messages (Shturmap shows no rewards), server region and raid short id, graphics and
  sound settings.
- Not in the allowed logs: survived or killed, kills, loot, XP, money, inventory, a continuous position.
