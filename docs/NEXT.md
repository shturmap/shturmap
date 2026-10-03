# Next: more from the game's logs

Plan for the next session (written 2026-10-01, evening). The owner asked for items 1–4 below; they come from a
read of everything in their own application and push-notification logs (20 sessions, 2026-08-15 to 2026-10-01).
Item 5 (one design system) was added on 2026-10-02, item 6 (no game installed) on 2026-10-03. Items 1, 3 and 4 are done
(2026-10-02), item 5 on 2026-10-03; 2 and 6 are open.
`docs/DESIGN.md` stays the binding spec: update it in the same change as each item.

## Queued for Monday, 2026-10-05 (owner, 2026-10-03: record now, implement when there's quota again)

**Status, 2026-10-03 evening:** the quota came back the same day and the owner said "go ahead with the open TODOs".
Done and merged (DESIGN.md has each decision): 1, 2, 3, 5, 6, 7, 8, 9, 11, 12, 13, 14 (with the changelog the
owner added), 15, 16, and 17 (the design system, §4). Still open:
- **4, click checks:** in the installed dev build ("Shturmap DEV", F12 for the developer view), with the owner.
- **10, the FAQ:** drafted, not published. The website changes are on the site repository's branch `work/faq`
  (worktree `..\Shturmap-wt\g-site`, with the design system's CSS alignment on top), and the README FAQ on the app
  branch `work/g-readme` (worktree `..\Shturmap-wt\g-readme`). They wait for the owner's choice of heading ("Is this
  allowed? Can I get banned?" or "Is this cheating?") and their go. Publish both together: the README links to the
  website's `#faq`.
- **18:** items 2 and 6 below, and the release checklist (on hold until the release).
- Version: still 0.2.0, though much has changed since the build sent to a friend; ask before the next one goes out.
- **Running:** tarkov.dev's tile renders for The Lab, Labyrinth and Icebreaker (owner, 2026-10-03, "Do B and A":
  map renders accepted like portraits and icons), and landmarks (locked doors with their keys, switches, container
  dots on the sheet).
- **Next, after the map work is merged** (owner, 2026-10-03): "Uninstall Shturmap…" in help, with one confirmation
  and an unticked "Also delete my Shturmap data" (settings, quest history, logs, study log, download cache), which
  then runs Velopack's own uninstaller (`Update.exe --uninstall`); the same for the dev build with its own data
  folder; one line in README and FAQ ("Windows Settings → Apps, or Help → Uninstall"); PRIVACY.md can then say how
  to delete everything. Windows' Apps entry already works (Velopack registers it); the Setup itself has no
  repair/remove mode, and a different installer around Velopack isn't worth it.
- **Then, once everything is done: the website's media from the newest version** (owner, 2026-10-03: "Once
  everything is done, update the website with assets from the most recent version"). Re-record with
  `tools\make-media.ps1` (site repo; its CLAUDE.md and hand-check list) on the branch `work/faq`, so the website
  goes out as one update: media, FAQ, release wording, design-system CSS. Going live needs the owner's FAQ heading
  choice; publish the site and merge `work/g-readme` together.

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
   Updates setting, the study-log switch, Copy diagnostics, LOG FOLDER, LICENCES, PRIVACY, the report dialog's Send,
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
      taken); do I need to know the maps; how the position works (press your screenshot key); PvE and PvP; which
      maps; game languages; what it costs (nothing) and what it sends (only reports you send, the update check).
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
- Once the settings flyout (help / settings split) is on main, add a "GAME FOLDER: <path or 'not found'> ·
  CHOOSE…" row there, so a found-but-wrong install can be changed too.

## At the production release on GitHub (owner, 2026-10-03)

Raise these with the owner when the app gets its first public GitHub Release (not for builds passed to friends):

1. **Website media.** Re-record the screenshots and the hero clip (`tools\make-media.ps1` in the site repository),
   only when the owner says so.
2. **Reporting is one route: in the app, to Sentry** (owner, 2026-10-03: "don't want to mix github issues and other
   reporting methods, it should be one coherent easy to use thing"; done, DESIGN.md §8, "Reports"). This replaces
   the earlier plan of GitHub issue forms plus a separate web form. Before the release:
   - the release is built with the DSN (`eng\sentry.dsn`, untracked; `eng\release.ps1` warns without it), and
     `Shturmap.exe --send-report "<text>" <folder>` from the release exe says "Sent. Thank you.";
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
