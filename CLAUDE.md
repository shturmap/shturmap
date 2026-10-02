# Shturmap

Read `docs/DESIGN.md` before changing anything. It is binding: product scope (one job, no feature bloat), the
game terms-of-service boundary, copyright rules, UX principles (second monitor, no clicks in a raid, minimal),
the visual language and taxonomy, and engineering rules. If a change conflicts with it, stop and ask the owner;
if a decision changes, update `docs/DESIGN.md` in the same change.

Essentials:

- Never touch the game process, send input, hook, register global hotkeys or capture the screen. `SafetyTests`
  enforces this.
- Never bundle tarkov.dev data, map artwork or Battlestate art; download at runtime, credit, personal use only.
- Never capture the user's monitors to check UI; use `Shturmap.exe --snapshot <folder>` or `shturmap-cli render`.
- Build and test: `.\eng\dotnet.ps1 build Shturmap.slnx`, `.\eng\dotnet.ps1 test --solution Shturmap.slnx`
  (the wrapper finds the per-user .NET 10 SDK). Publish: `.\eng\publish.ps1`.
- The CLI (`tools/Shturmap.Cli`) runs every part headless; `simulate` plays a scripted raid against a fake game.
- After UI changes that show in the website's screenshots or hero clip, re-record them with `tools\make-media.ps1`
  in `..\shturmap.github.io` (see its CLAUDE.md), then go through its hand-check list.
