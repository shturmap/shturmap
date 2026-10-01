# Spotter

A Windows companion for Escape from Tarkov. It shows where you are on the map, what your active quests want
from that map, and how far away and in which direction each objective and extract is. It is built to sit on a
second monitor and to need no clicks during a raid: your screenshot key is the only input.

It reads only what the game writes for you: screenshot file names, the application and notification logs, and
the game's Control.ini and Game.ini. It never touches the game process, never sends input, never captures the
screen and never draws over the game. See [docs/DESIGN.md](docs/DESIGN.md#ground-rules).

## Run it

```powershell
.\eng\publish.ps1                 # builds a self-contained folder
.\artifacts\Spotter\Spotter.exe   # no .NET install needed to run it
```

On first start Spotter finds the game (Steam or the Battlestate Games launcher), the logs, the Screenshots
folder, your screenshot key and the game language. It downloads quest and map data from tarkov.dev, reads all
log sessions on disk for quest history, imports TarkovEyes progress if present, and reads any Tasks-screen
screenshots from the last week. The window opens maximised on the second monitor.

In a raid, press your screenshot key (PrtSc by default). The map switches to the raid's map when it loads; each
screenshot moves your marker, picks the floor from your height and re-sorts objectives by distance. To update
your quest list, open Character → Tasks and press the screenshot key; scroll and press again for more rows.

## Work on it

Requires the .NET 10 SDK. `eng\dotnet.ps1` finds an SDK installed per user when the machine-wide `dotnet` has none.

```powershell
.\eng\dotnet.ps1 build Spotter.slnx
.\eng\dotnet.ps1 test --solution Spotter.slnx
.\eng\dotnet.ps1 run --project tools\Spotter.Cli -- help
```

`tools/Spotter.Cli` runs the pieces without the UI or the game:

| command | what it does |
| --- | --- |
| `locate` | shows every install candidate found and which one is used |
| `replay [session]` | replays a log session through the raid tracker |
| `data [mode] [lang]` | loads tarkov.dev data and lists maps |
| `ocr <png>…` | reads Tasks-screen screenshots and matches quest names |
| `render <map> <out.png> [screenshot names…]` | draws a map with positions to a PNG |
| `watch [seconds]` | runs the companion headless and prints what it sees |
| `simulate` | plays a scripted raid against a temporary fake game folder |

`Spotter.exe --snapshot <folder> [seconds]` renders the window and the map to PNGs and exits.

Diagnostics are written to `%LOCALAPPDATA%\Spotter\logs`. Data lives in `%LOCALAPPDATA%\Spotter`.

## Credits and licences

- Quest, map and item data: [tarkov.dev](https://tarkov.dev) (json.tarkov.dev).
- Map geometry: tarkov.dev's `maps.json` (MIT).
- Map artwork: Shebuka and contributors, [tarkov-dev-svg-maps](https://github.com/the-hideout/tarkov-dev-svg-maps),
  CC BY-NC-SA 4.0. Downloaded at runtime for personal use, never bundled.
- Escape from Tarkov is a trademark of Battlestate Games. Spotter is unofficial and unaffiliated.

Personal, non-commercial use.
