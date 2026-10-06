# Third-party notices

Shturmap's own code is under the MIT licence (`LICENSE`). A build of Shturmap also contains the components below,
each under its own licence. Every build carries the full licence and notice texts, this file and `LICENSE` in its
`licenses` folder (`eng\notices.ps1` gathers them from the packages), installed with the app, and the help
panel's LICENCES link opens the folder. Data and artwork that Shturmap
downloads at runtime are not part of a build; they are credited in the [README](README.md#credits).

| Component | Licence | Copyright |
| --- | --- | --- |
| .NET runtime 10.0 (self-contained) | MIT | .NET Foundation and Contributors |
| Windows App SDK 2.5 (WinUI 3, Windows App Runtime, DWriteCore) | Microsoft Software License Terms for the Windows App SDK | Microsoft Corporation |
| Microsoft.Windows.SDK.NET, WinRT.Runtime | Windows SDK licence terms (<https://aka.ms/WinSDKLicenseURL>) | Microsoft Corporation |
| Microsoft Edge WebView2 SDK 1.0.3719.77 (comes with WinUI) | BSD-3-Clause-style | Microsoft Corporation |
| CommunityToolkit.Mvvm 8.4.2 | MIT | .NET Foundation and Contributors |
| SkiaSharp and SkiaSharp.Views.WinUI 4.153.1 (Skia, ANGLE) | MIT | Xamarin, Inc.; Microsoft Corporation |
| HarfBuzzSharp 14.2.0 (HarfBuzz) | MIT | Microsoft Corporation |
| Svg.Skia, Svg.Model, Svg.Animation, Svg.SceneGraph, ShimSkiaSharp 5.2.3 | MIT | Wiesław Šoltés |
| Svg.Custom 5.2.3 (built from SVG.NET) | Microsoft Public License (MS-PL) | Wiesław Šoltés; SVG.NET contributors |
| ExCSS 4.3.1 | MIT | Tyler Brinks |
| Microsoft.Data.Sqlite 10.0.12 | MIT | Microsoft Corporation |
| SQLitePCLRaw 2.1.12 | Apache License 2.0 | SourceGear, LLC |
| SQLite (e_sqlite3) | Public domain | |
| System.Numerics.Tensors 9.0.0 | MIT | .NET Foundation and Contributors |
| Sentry 6.12.0 (only its report format; reports are sent by Shturmap's own code, see PRIVACY.md) | MIT | Sentry |
| Velopack 1.2.161 (the Setup, Update.exe and the update library) | MIT | Velopack Ltd, Caelan Sayler, Kevin Bost |
| Phosphor Icons (eight icons, the quest types' glyphs and the hand-over's arrow, as path data in the code) | MIT | Phosphor Icons |

The Windows App SDK runtime files in a build are licensed by Microsoft under the terms in
`licenses\Microsoft.WindowsAppSDK\license.txt`, not under Shturmap's MIT licence. Using or passing on a build means
accepting those terms for those files. They note that the runtime may send diagnostic data to Microsoft; Shturmap's
own code sends nothing unless the player sends a report or allows crash reports ([PRIVACY.md](PRIVACY.md)).

The native Skia, HarfBuzz and ANGLE libraries contain further open-source code (FreeType, libjpeg-turbo, libpng,
libwebp, zlib, expat, ICU and others), listed with their licences in
`licenses\SkiaSharp.NativeAssets.Win32\THIRD-PARTY-NOTICES.txt`. As their licences ask:

- This software is based in part on the work of the Independent JPEG Group.
- Portions of this software are copyright © The FreeType Project (www.freetype.org). All rights reserved.

## MIT notices for packages and artwork that ship no licence file

- ExCSS: Copyright (c) 2024 Tyler Brinks.
- Svg.Skia, Svg.Model, Svg.Animation, Svg.SceneGraph, ShimSkiaSharp: Copyright (c) 2020 Wiesław Šoltés.
- Microsoft.Data.Sqlite: Copyright (c) Microsoft Corporation.
- Sentry: Copyright (c) 2018 Sentry.
- Velopack: Copyright (c) Velopack Ltd. (The Setup and Update.exe are built by Velopack's `vpk` from its open-source
  code and the Rust libraries it uses, listed with their licences at <https://github.com/velopack/velopack>.)
- Phosphor Icons: Copyright (c) 2023 Phosphor Icons. (<https://github.com/phosphor-icons/core>; the icons crosshair,
  hand, push-pin, package, person-simple-run, handshake and arrow-right in the "fill" weight, and magnifying-glass in
  "bold".)

Each is licensed under these terms:

> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
> documentation files (the "Software"), to deal in the Software without restriction, including without limitation
> the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and
> to permit persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of
> the Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO
> THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
> AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
> CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
> IN THE SOFTWARE.

Svg.Custom is under the MS-PL (`licenses\MS-PL.txt`); SQLitePCLRaw is under the Apache License 2.0
(`licenses\Apache-2.0.txt`) with the notices in `licenses\SQLitePCLRaw-NOTICE.txt`.
