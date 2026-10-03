<h1>
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="brand/logo-dark.svg">
    <img alt="Shturmap" src="brand/logo-light.svg" height="40">
  </picture>
</h1>

> **In private testing.** Expect rough edges. If something is wrong or missing, tell us from the app: the
> **feedback** button at the top right.

**A second-monitor map for Escape from Tarkov.** Your active quests' objectives, the extracts and your position on
the map of the raid you're in: what you'd otherwise look up in the wiki, in one window. Website:
[shturmap.github.io](https://shturmap.github.io)

Quest status comes from the game's own log files. Your position comes from the name of the screenshot the game saves
when you press your screenshot key. No macros, no input to the game, no access to its process.

<img alt="Shturmap in a raid on Streets of Tarkov: the raid card with the next objective and the nearest extract by distance and direction, and the map with quest markers and your position" src="https://shturmap.github.io/assets/img/raid.webp" width="800">

<sub>A raid on Streets of Tarkov, rendered by the app. Map © Shebuka and contributors, CC BY-NC-SA 4.0.</sub>

- **Before the raid:** maps ranked by how many of your quests they finish or move on, and what to bring, with where
  to get it. Pick the quests you want to tackle with the pen beside them: they come first and stay lit on the map
  until they are done.
- **In the raid:** press your screenshot key and your marker drops; objectives and the nearest extract are sorted by
  distance and direction. No clicks.
- **Quest cards:** objectives, keys and items for any quest, one hover away. Pop out a card and its distances stay
  live.
- **Quest tracking:** started, finished, failed, read from the game's own log files. Nothing to tick off.
- **Native Windows app:** C# on .NET and WinUI 3, with the map drawn on your GPU. No Electron; it opens in under a
  second.

## What it reads, and what it never does

Shturmap works only from files the game writes for you and from public community data:

- the **file names** of your screenshots, which the game fills with your position and facing (the images are
  never read). Battlestate's own support article on
  [reporting a bug](https://www.escapefromtarkov.com/support/knowledge/437) describes these coordinates in the name;
  that is where they come from, not an endorsement of Shturmap;
- the game's `application` and `push-notifications` logs, read-only: the map you load into, raid start and end,
  quests started, failed and completed;
- `Control.ini` and `Game.ini`, read-only: your screenshot key and the game language;
- where the game is installed: the Windows uninstall entry, Steam's library list, and the games folder from the
  Battlestate Games launcher's settings (nothing else from that file);
- quest, map and item data from tarkov.dev, and the community map artwork.

It never:

- opens the game process, reads or writes its memory, or loads anything into it;
- sends keystrokes or mouse input, or registers global hotkeys (its shortcuts work only in its own window);
- captures the screen or draws over the game;
- changes game files or settings, or reads the launcher's login data or the game's backend log;
- sends anything about you or your game, unless you send a report or allow crash reports (below). Otherwise its
  only network traffic downloads the public data above and asks GitHub for a newer version of Shturmap at start
  (turn that off in settings, under Updates), and what it records stays in `%LOCALAPPDATA%\Shturmap`: a short app log,
  and a study log of how you use it only if you switch that on in settings. (The Microsoft runtime it is built on has
  its own terms; see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).)

To report a problem or suggest an idea, use the **feedback** button at the top right: write what happened, keep
"Include diagnostics" ticked (Show what's sent shows exactly what goes, with your user folder masked and no ids),
and press Send. No account needed. After a crash, Shturmap asks at its next start whether to send a crash report;
"Crash reports" in settings (the gear beside ?) can make that Always or Never. What a report holds and who receives it:
[PRIVACY.md](PRIVACY.md).

A test fails if the code ever calls the Windows APIs for reading other processes, sending input, hooking or
capturing the screen. The full rules are in [docs/DESIGN.md](docs/DESIGN.md#2-ground-rules-game-terms-of-service).

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
objectives and the extracts.

More questions and answers: [shturmap.github.io/#faq](https://shturmap.github.io/#faq).

## Get it

Download **`Shturmap-Setup.exe`** from the [releases](https://github.com/shturmap/shturmap/releases) (marked
pre-release while Shturmap is in private testing), for 64-bit Windows 10 (2004) or later. It installs for your
Windows user only, with no admin rights, into `%LOCALAPPDATA%\ShturmapApp`, with a Start-menu and a desktop
shortcut. The installer isn't signed, so Windows warns about it; click **More info → Run anyway**.

Uninstall: Windows Settings → Apps, or settings (the gear) → **Uninstall Shturmap…**. Your data in `%LOCALAPPDATA%\Shturmap`
(settings, quest history, logs, study log, download cache) stays for a later install, unless you tick **Also delete
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
- Bundled libraries and their licences: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Licence

Shturmap's code is under the [MIT licence](LICENSE). Data and artwork it downloads keep their own terms (see
Credits).
