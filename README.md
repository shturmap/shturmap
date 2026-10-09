<h1>
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="brand/logo-dark.svg">
    <img alt="Shturmap" src="brand/logo-light.svg" height="40">
  </picture>
</h1>

[![CI](https://github.com/shturmap/shturmap/actions/workflows/ci.yml/badge.svg)](https://github.com/shturmap/shturmap/actions/workflows/ci.yml)
[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/shturmap/shturmap/badge)](https://scorecard.dev/viewer/?uri=github.com/shturmap/shturmap)

> [!NOTE]
> **Shturmap is in private testing.** Its releases are marked pre-release, and there are rough edges.

**A second-monitor map for Escape from Tarkov.** Your active quests' objectives, the extracts and your position on
the map of the raid you're in: what you'd otherwise look up in the wiki, in one window.

**[Download for Windows](https://github.com/shturmap/shturmap/releases)** ·
[Website](https://shturmap.github.io) ·
[What it reads, and what it never does](#what-it-reads-and-what-it-never-does) ·
[Before you install](#before-you-install) ·
[Check it yourself](#check-it-yourself)

- **What it does.** Before a raid it ranks the maps by how many of your active quests they finish or move on, and
  lists what to bring. In the raid it shows your objectives and the ways out on the map, each with its distance
  and direction from the spot where you took your last screenshot.
- **How it knows.** Quest status comes from the game's own log files. Your position comes from the name of the
  screenshot the game saves when you press your screenshot key, and your extracts for the raid from the game's own
  list, when that screenshot shows it. Quest, map and item data come from tarkov.dev.
- **What it leaves alone.** No macros, no input to the game, nothing read from its memory, nothing drawn over it.
- **What you need.** 64-bit Windows 10 (2004) or later. Free, no account, MIT licence.
- **Where it stands.** In private testing: its releases are marked pre-release, and there are rough edges. If
  something is wrong or missing, tell us from the app with the **feedback** button at the top right, or open an
  [issue on GitHub](https://github.com/shturmap/shturmap/issues).
- **The risk.** An unofficial fan project, not made or endorsed by Battlestate Games. There is no guarantee against
  sanctions, so you use it at your own risk; [Before you install](#before-you-install) says why.

<img alt="Shturmap in a raid on Streets of Tarkov: the raid card with the time left, the next objective and the nearest extract on your list by distance and direction, and the map with quest markers, your extracts and your position" src="https://shturmap.github.io/assets/img/raid.webp" width="800">

<sub>A raid on Streets of Tarkov, rendered by the app. Map © Shebuka and contributors, CC BY-NC-SA 4.0.</sub>

- **Before the raid:** maps ranked by how many of your quests they finish or move on, and what to bring, with where
  to get it. Pick the quests you want to tackle with the pen beside them: they come first and stay lit on the map
  until they are done.
- **In the raid:** press your screenshot key and your marker drops; objectives and the nearest extract are sorted by
  distance and direction, with the time the raid still runs above them. No clicks.
- **Your extracts:** the game gives each raid its own extracts and shows them in a list at the top right, at the
  raid's start and when you ask for it (O twice by default). Take a screenshot while it shows and Shturmap reads it:
  the extracts on your list light up on the map and come first, the others turn hollow. Until then it says that
  the nearest extract isn't checked against your list.
- **After the raid:** with a few screenshots taken, the map replays where you took them, in order, faint at the
  raid's start and bright at its end. REPLAY plays it again until your next raid. Nothing of it is saved.
- **Quest cards:** objectives, keys and items for any quest, one hover away. Pop out a card and its distances stay
  live.
- **Quest tracking:** started, finished, failed, read from the game's own log files. Nothing has to be ticked off.
  The logs don't say when a single objective is done, so you can tick one on its quest's card if you want the map
  to stop leading you there.
- **Native Windows app:** C# on .NET and WinUI 3, with the map drawn on your GPU. No Electron; it opens in under a
  second.

## What it reads, and what it never does

Shturmap works only from files the game writes for you and from public community data:

- the **file names** of your screenshots, which the game fills with your position and facing. Battlestate's own
  support article on
  [reporting a bug](https://www.escapefromtarkov.com/support/knowledge/437) describes these coordinates in the name;
  that is where they come from, not an endorsement of Shturmap;
- of a screenshot you take in a raid, **the top right corner of its picture**: if the game's extract list shows
  there, Shturmap reads which extracts are yours this raid, with the text recognition built into Windows. It happens
  on your PC, nothing of the picture is kept or sent, and no other part of any picture is looked at. Untick **Read
  the extract list from screenshots** in settings and no picture is opened at all;
- the game's `application` and `push-notifications` logs, read-only: the map you load into, raid start and end,
  quests started, failed and completed;
- `Control.ini` and `Game.ini`, read-only: your screenshot key and the game language;
- where the game is installed: the Windows uninstall entry, Steam's library list, and the games folder from the
  Battlestate Games launcher's settings (nothing else from that file);
- quest, map and item data from tarkov.dev, and the community map artwork.

It never:

- reads or writes the game's memory, or loads anything into the game;
- opens the game's process from its own code (its installer is the one exception, said below);
- sends keystrokes or mouse input, or registers global hotkeys (its shortcuts work only in its own window);
- captures the screen or draws over the game (the pictures it looks at are screenshots you took yourself);
- changes game files or settings, or reads the launcher's login data or the game's backend log;
- changes or deletes anything outside its own folders, with one exception you turn on yourself: **Delete position
  screenshots** in settings (off unless you tick it) deletes each screenshot that gave a position 5 seconds after
  its name was read, for good. Screenshots already in the folder, and screenshots from the menus, stay;
- sends anything about you or your game, unless you send a report or allow crash reports (below). Otherwise its
  only network traffic downloads the public data above and asks GitHub for a newer version of Shturmap at start
  (turn that off in settings, under Updates), and what it records stays in `%LOCALAPPDATA%\Shturmap`: a short app log.
  (The Microsoft runtime it is built on has its own terms; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).)

To report a problem or suggest an idea, use the **feedback** button at the top right: write what happened, keep
"Include diagnostics" ticked (Show what's sent shows exactly what goes, with your user folder masked and no ids),
and press Send. No account needed. After a crash, Shturmap asks at its next start whether to send a crash report;
"Crash reports" in settings (the gear beside ?) can make that Always or Never. What a report holds and who receives it:
[PRIVACY.md](PRIVACY.md).

With a GitHub account you can also open an [issue](https://github.com/shturmap/shturmap/issues). Issues are public:
don't post the game's logs, screenshots or ids from your game there. The app's report leaves those out.

**The installer and running programs.** Shturmap is installed, updated and removed by
[Velopack](https://velopack.io), an open-source installer that comes with it. While it installs Shturmap, applies an
update (at Shturmap's start when one was downloaded, or on RESTART NOW) or uninstalls it, Velopack goes through the
programs running on the PC to close running copies of Shturmap. To do that it asks Windows for a handle to each
running program, with the rights to read where its file is and to end it, and it ends only programs that run from
Shturmap's own folder. If the game is running at that moment, it is asked about like every other program; nothing is
read from it, written to it or sent to it. To keep even this away from a running game, install Shturmap and let it
update while the game is closed (under Updates in settings, "Tell me only" lets you choose the moment). If another program
keeps Shturmap's folder in use, an update can't be applied and the old version starts again; Shturmap says so. Close
that program and restart Shturmap.

A test fails if Shturmap's own code ever calls the Windows APIs for opening or reading other processes, sending
input, hooking or capturing the screen. The full rules are in
[docs/DESIGN.md](docs/DESIGN.md#2-ground-rules-game-terms-of-service); how to check the code and your download:
[Check it yourself](#check-it-yourself).

## Before you install

**Can I get banned for using it?** Battlestate hasn't said anything about tools that read screenshot names, for or
against, and we found no verified ban for using one (our research as of 3 October 2026, not a promise). But its
License Agreement (section 4.3.4) forbids outside software that "captures, collects, counts or otherwise 'retrieves'
information reproduced or stored by" the game unless Battlestate permits it, it hasn't permitted any tool like this,
and it discourages third-party software in general. So there is no guarantee against sanctions: use it at your own
risk. Shturmap is an unofficial fan project, not made or endorsed by Battlestate Games.

**Is it fair in PvP?** It shows your position to you only: it never shares it with your squad or anyone else, and it
never takes a screenshot for you. Some players still see a map with your own position as an unfair advantage in PvP;
Shturmap is at its most useful for learning maps and in PvE.

**What does it send?** Nothing about you or your game, unless you send a report or allow crash reports (above). Its
other traffic downloads public data and map artwork, and asks GitHub for a newer version (turn that off in settings,
under Updates).

**What if Battlestate takes the coordinates out of the screenshot names?** Then Shturmap shows no position, and the
rest keeps working: maps ranked by what you can get done, what to bring, quest cards, and the map with your
objectives and the extracts. (A screenshot without a position in its name isn't looked at for the extract list
either.)

**Does it show the story chapters?** Not yet. Its quests come from tarkov.dev, which doesn't publish the story
chapters (Falling Skies, Batya, …), so Shturmap shows the traders' quests only.

More questions and answers: [shturmap.github.io/#faq](https://shturmap.github.io/#faq).

## Check it yourself

What Shturmap does doesn't rest on our word alone. You can check:

- **What its code may not do.** The tests run on every push, and the
  [runs](https://github.com/shturmap/shturmap/actions/workflows/ci.yml) are public.
  [`SafetyTests`](tests/Shturmap.Core.Tests/SafetyTests.cs) fails if Shturmap's own code (`src/`) uses the Windows
  APIs for opening or reading other processes, sending input, hooking, registering hotkeys or capturing the screen,
  or names a web address other than tarkov.dev's and GitHub's.
  [`AllowedHostsTests`](tests/Shturmap.Data.Tests/AllowedHostsTests.cs) checks that its download client asks only
  `json.tarkov.dev`, `assets.tarkov.dev` and `raw.githubusercontent.com`, over https, and refuses any other address
  before a request is sent. These check the source code; what ties your download to that source is the next point.
- **Where your download comes from.** From 0.4.0, each release is built and published by the repository's
  [Release workflow](.github/workflows/release.yml) on GitHub's servers, from the commit its tag names, not on
  anyone's PC. Before the release is published, the workflow records a signed build provenance attestation for the
  Setup and each package: the repository, the workflow and the commit that built it. With the
  [GitHub CLI](https://cli.github.com), signed in to any GitHub account:

  ```powershell
  gh attestation verify Shturmap-Setup.exe -R shturmap/shturmap
  ```

  It succeeds only for a file that a workflow of this repository built, and names the workflow. These releases also
  carry `Shturmap-Setup.exe.sha256`; compare it with `Get-FileHash Shturmap-Setup.exe`. The hash shows that the
  file arrived whole, the attestation where it came from.
- **What it needs and talks to.** The Setup installs for your Windows user only, without admin rights, into
  `%LOCALAPPDATA%\ShturmapApp`. The app downloads data and artwork from the three hosts above only and asks GitHub
  (`api.github.com`, and GitHub's hosts for release files) for updates; a report you send, or a crash report you
  allow, goes to Sentry (`sentry.io`). One way to try a program away from your own Windows is Windows Sandbox
  (Windows 10 and 11 Pro, Enterprise and Education), which starts empty and is discarded when you close it; we
  haven't tried Shturmap in it yet.
- **Who writes it.** Shturmap is written with an AI coding assistant (Claude, by Anthropic), credited as co-author in
  the commits. What keeps it in check: the [design document](docs/DESIGN.md) the code has to follow, the tests, and
  CI on every push.

## Get it

Download **`Shturmap-Setup.exe`** from the [releases](https://github.com/shturmap/shturmap/releases) (marked
pre-release while Shturmap is in private testing), for 64-bit Windows 10 (2004) or later. It installs for your
Windows user only, with no admin rights, into `%LOCALAPPDATA%\ShturmapApp`, with a Start-menu and a desktop
shortcut. The installer isn't signed, so Windows warns about it; click **More info → Run anyway**.

Uninstall: Windows Settings → Apps, or settings (the gear) → **Uninstall Shturmap…**. Your data in `%LOCALAPPDATA%\Shturmap`
(settings, quest history, logs, download cache) stays for a later install, unless you tick **Also delete
my Shturmap data** in its question.

Shturmap keeps itself up to date: it asks GitHub for a newer version at start and every 6 hours, downloads it in
the background and applies it the next time you start it, never during a raid. Settings (the gear) → **Updates** switches
between Automatic, Tell me only and Off.

To build it yourself, with the [.NET 10 SDK](https://dotnet.microsoft.com/download):

```powershell
.\eng\release.ps1                     # the Setup and update packages (Velopack): artifacts\release
.\eng\publish.ps1                     # or just the folder build: artifacts\Shturmap\Shturmap.exe
.\eng\dev.ps1                         # the dev build "Shturmap DEV", installed beside the release: artifacts\dev
```

Builds other than the installed release keep their data in `%LOCALAPPDATA%\Shturmap-dev`, apart from yours.

On first start it finds the game (Steam or the Battlestate Games launcher), its logs and your Screenshots folder,
downloads data from tarkov.dev and reads your existing logs for quest history. In a raid, press your screenshot
key: your marker moves, the floor follows your height and objectives re-sort by distance. If it doesn't find the
game, it says so in place of the raid plan: use **Choose game folder…** there, or browse the maps meanwhile.

Limits: quests come from the logs alone, so a quest started before your oldest log isn't known. The Lab, Labyrinth
and Icebreaker are drawn from tarkov.dev's top-down renders, loaded as you look at them; without a connection and
saved tiles they fall back to a 10 m grid with your position, objectives and extracts. On Windows 10, install the
Segoe Fluent Icons font for the glyphs.

## Develop

```powershell
.\eng\dotnet.ps1 build Shturmap.slnx
.\eng\dotnet.ps1 test --solution Shturmap.slnx
.\eng\dotnet.ps1 run --project tools\Shturmap.Cli -- help   # every part headless, incl. a simulated raid
```

[docs/DESIGN.md](docs/DESIGN.md) is the binding spec; read it before changing anything.

## Credits

- Quest, map and item data, map geometry and image hosting: [tarkov.dev](https://tarkov.dev) by
  [The Hideout](https://github.com/the-hideout).
- Map artwork: Shebuka and contributors,
  [tarkov-dev-svg-maps](https://github.com/the-hideout/tarkov-dev-svg-maps),
  [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/). Downloaded at runtime, never bundled,
  non-commercial use only.
- The Lab, Labyrinth and Icebreaker: tarkov.dev's top-down renders (Icebreaker's by
  [TarkovBOT.eu](https://tarkovbot.eu/)) of Battlestate's levels, shown from tarkov.dev's image service at runtime
  like trader portraits and item icons, never bundled.
- Inspired by [TarkovEyes](https://github.com/MelGP/tarkoveyes) by MelGP; Shturmap is a from-scratch rebuild.
- Log formats and file locations were learned from community tools:
  [TarkovMonitor](https://github.com/the-hideout/TarkovMonitor),
  [TarkovTracker](https://github.com/tarkovtracker-org/TarkovTracker), [MAYAK](https://github.com/ichi0g0y/mayak),
  [Tarkov Pilot](https://github.com/ggdiam/TarkovPilot) and [RatScanner](https://github.com/RatScanner/RatScanner).
  No code was taken from them.
- Quest and interactive-map links go to the [Escape from Tarkov Wiki](https://escapefromtarkov.fandom.com).
- Escape from Tarkov and its game content and materials are trademarks and copyrights of Battlestate Games and its
  licensors. Trader portraits and item icons are Battlestate's art, shown from tarkov.dev at runtime and never
  bundled.
- The quest types' icons: [Phosphor Icons](https://phosphoricons.com), MIT licence.
- Bundled libraries and their licences: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Licence

Shturmap's code is under the [MIT licence](LICENSE). Data and artwork it downloads keep their own terms (see
Credits).
