<h1>
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="brand/logo-dark.svg">
    <img alt="Shturmap" src="brand/logo-light.svg" height="40">
  </picture>
</h1>

**A second-monitor map for Escape from Tarkov.** Your active quests' objectives, the extracts and your position on
the map of the raid you're in: what you'd otherwise look up in the wiki, in one window. Website:
[shturmap.github.io](https://shturmap.github.io)

Quest status comes from the game's own log files. Your position comes from the name of the screenshot the game saves
when you press your screenshot key. No macros, no input to the game, no access to its process.

<img alt="Shturmap in a raid on Streets of Tarkov: the raid card with the next objective and the nearest extract by distance and direction, and the map with quest markers and your position" src="https://shturmap.github.io/assets/img/raid.webp" width="800">

<sub>A raid on Streets of Tarkov, rendered by the app. Map © Shebuka and contributors, CC BY-NC-SA 4.0.</sub>

- **Before the raid:** maps ranked by how many of your quests they finish or move on, and what to bring, with where
  to get it.
- **In the raid:** press your screenshot key and your marker drops; objectives and the nearest extract are sorted by
  distance and direction. No clicks.
- **Quest cards:** objectives, keys and items for any quest, one hover away. Pin a card and its distances stay live.
- **Quest tracking:** started, finished, failed, read from the game's own log files. Nothing to tick off.
- **Native Windows app:** C# on .NET and WinUI 3, with the map drawn on your GPU. No Electron; it opens in under a
  second.

## What it reads, and what it never does

Shturmap works only from files the game writes for you and from public community data:

- the **file names** of your screenshots, which the game fills with your position and facing (the images are
  never read);
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
- sends anything about you or your game anywhere. Its only network traffic downloads the public data above, and
  what it records stays in `%LOCALAPPDATA%\Shturmap`. (The Microsoft runtime it is built on has its own terms; see
  [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).)

A test fails if the code ever calls the Windows APIs for reading other processes, sending input, hooking or
capturing the screen. The full rules are in [docs/DESIGN.md](docs/DESIGN.md#2-ground-rules-game-terms-of-service).

Shturmap is an unofficial fan project, not made or endorsed by Battlestate Games. Battlestate discourages
third-party software in general, so use it at your own risk.

## Get it

There are no release builds yet. Build it on Windows 10 (2004) or later with the
[.NET 10 SDK](https://dotnet.microsoft.com/download):

```powershell
.\eng\publish.ps1                     # self-contained build in artifacts\Shturmap
.\artifacts\Shturmap\Shturmap.exe
```

On first start it finds the game (Steam or the Battlestate Games launcher), its logs and your Screenshots folder,
downloads data from tarkov.dev and reads your existing logs for quest history. In a raid, press your screenshot
key: your marker moves, the floor follows your height and objectives re-sort by distance.

Limits: quests come from the logs alone, so a quest started before your oldest log isn't known. Maps without
vector artwork (The Lab, Labyrinth, Icebreaker) aren't drawn yet. On Windows 10, install the Segoe Fluent Icons
font for the glyphs.

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
