# Shturmap brand

The Shturmap logo: a stencilled Ш with a "this way up" chevron cut through its centre stem, and the wordmark SHTUR
in ink with MAP cut out of an amber strip. Everything here is built by `build.cs`; don't edit the files by hand.

## Files

| File | Use |
| --- | --- |
| `icon.svg` | The icon, detailed, for 48 px and up |
| `icon-small.svg` | The plain Ш for 16 to 40 px, without the chevron and corner marks |
| `logo-dark.svg`, `logo-light.svg` | Icon and wordmark, for dark and light backgrounds |
| `wordmark-dark.svg`, `wordmark-light.svg` | The wordmark alone |
| `icon-512.png`, `icon-1024.png` | The icon as raster, for the website and stores |
| `logo-dark.png`, `logo-light.png` | The logos at 80 px tall (twice README size), as raster fallbacks |
| `../src/Shturmap.App/Assets/Shturmap.ico` | The app icon: the small drawing fitted to 16, 20, 24, 32 and 40 px, the detailed one at 48 to 256 px |

The SVGs are plain filled paths over a viewBox (no masks, text or fonts), so they look the same everywhere.

In a README:

```html
<picture>
  <source media="(prefers-color-scheme: dark)" srcset="brand/logo-dark.svg">
  <img alt="Shturmap" src="brand/logo-light.svg" height="40">
</picture>
```

## Colours

| | On dark | On light |
| --- | --- | --- |
| Amber (letter, MAP strip) | `#C9AD62` | `#8C7436` |
| Ink (SHTUR) | `#D9D5C4` | `#1E1F1B` |

The icon keeps its own colours on both: plate `#151614`, border `#45463F`, corner marks `#4A4A41`, letter `#C9AD62`.

## Construction

- The three stems are always joined to the base, all end flat at the same height, and nothing runs across the top.
  Cut free, three bars read as "III".
- The chevron is cut through the centre stem only, pointing up. Never on all three stems, never pointing down.
- The corner marks stay low-contrast, mirror each other and open toward the centre.
- The wordmark's M stays whole; stencil bridges only where a closed shape needs one (A, P, R).
- Lockup: the icon's plate is about 1.36 times the cap height, the gap between plate and wordmark is about half the
  cap height, and icon and wordmark are centred on one horizontal axis. The MAP strip reaches 8 % of the cap height
  above and below the letters.

## Rebuilding

```powershell
.\eng\dotnet.ps1 run brand\build.cs
```

It writes every file above, including the `.ico`. Change the drawing in `build.cs` and rebuild; commit the outputs.
