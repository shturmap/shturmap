# Puts PaddleOCR's recognition model and its dictionary into <Out>, for the app to carry in its ocr\ folder (the second
# reader of a label or countdown band Windows' recognition left unread; owner, 2026-10-26; docs/DESIGN.md §2 and §3,
# src\Shturmap.Game\Screenshots\PaddleReader.cs). Shturmap.Game's build runs it, so every build that has the reading has
# them (the app's publish, eng\publish.ps1, eng\release.ps1, eng\dev.ps1, the tests, the CLI, CI and the Release workflow).
# The files are never in the repository: they are fetched from RapidOCR's model hub (ModelScope) at a pinned revision,
# each checked against its SHA-256 before it is kept (the model's as RapidOCR's model list of that revision gives it,
# python/rapidocr/default_models.yaml), in a cache outside the repository that every checkout shares
# (%LOCALAPPDATA%\Shturmap-build\ocr\<revision>, or $env:SHTURMAP_BUILD_CACHE\ocr\<revision>), and copied from there
# only when its hash is still the pinned one. A build after the first needs no network. CI keeps that folder between
# runs, and the Release workflow restores CI's copy (actions/cache): a kept or restored file is checked here as a fetched
# one is, and fetched anew when its hash isn't the pinned one. A fetch is tried six times, 5, 10, 20, 30 and 45 s apart
# (2026-10-28: ModelScope answered GitHub's runners "502 Bad Gateway" for hours, and three tries 3 and 6 s apart stopped
# the release). Anything that fails stops the build: no build goes out without them.
# The model: PP-OCRv5 latin mobile recognition (PaddlePaddle's PaddleOCR, Apache-2.0), as RapidOCR exports it to ONNX
# (Apache-2.0), unmodified. To move to another: its URL and hash here and in eng\licenses\PaddleOCR-NOTICE.txt, its line
# in THIRD-PARTY-NOTICES.md, and the fallback measured again on the readers' fixtures (docs/UPDATES.md).
# Usage: .\eng\paddle-model.ps1 -Out <folder>
param([Parameter(Mandatory)] [string] $Out)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$revision = 'v3.10.0'
$hub = "https://www.modelscope.cn/models/RapidAI/RapidOCR/resolve/$revision"
$files = @(
  @{ Name = 'latin_PP-OCRv5_rec_mobile.onnx'; Url = "$hub/onnx/PP-OCRv5/rec/latin_PP-OCRv5_rec_mobile.onnx"
     Sha256 = 'B20BD37C168A570F583AFBC8CD7925603890EFBCDC000A59E22C269D160B5F5A' },
  @{ Name = 'ppocrv5_latin_dict.txt'; Url = "$hub/paddle/PP-OCRv5/rec/latin_PP-OCRv5_rec_mobile/ppocrv5_latin_dict.txt"
     Sha256 = '3C0A8A79B612653C25F765271714F71281E4E955962C153E272B7B8C1D2B13FF' }
)

$base = if ($env:SHTURMAP_BUILD_CACHE) { $env:SHTURMAP_BUILD_CACHE } else { Join-Path $env:LOCALAPPDATA 'Shturmap-build' }
$cache = Join-Path $base "ocr\$revision"
New-Item -ItemType Directory -Force $cache, $Out | Out-Null
# The hub's file host refuses a request that names no client; and Windows PowerShell may not offer TLS 1.2 by itself.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$agent = 'Shturmap-build (+https://github.com/shturmap/shturmap)'

# The SHA-256 by .NET, not Get-FileHash: Windows PowerShell's Get-FileHash comes from a script module, which can't load
# when the build is started from PowerShell 7 (as on GitHub's runner, whose pwsh hands its module path down to this
# powershell), and the build stopped with "The term 'Get-FileHash' is not recognized" (2026-10-26).
function Get-Sha256([string] $path) {
  $sha = [Security.Cryptography.SHA256]::Create()
  $stream = [IO.File]::OpenRead($path)
  try { -join ($sha.ComputeHash($stream) | ForEach-Object { $_.ToString('X2') }) }
  finally { $stream.Dispose(); $sha.Dispose() }
}

function Test-Hash([string] $path, [string] $sha256) {
  (Test-Path $path) -and (Get-Sha256 $path) -eq $sha256
}

foreach ($file in $files) {
  $kept = Join-Path $cache $file.Name
  if (-not (Test-Hash $kept $file.Sha256)) {
    # Fetched under a name of this run's own, checked, then moved in: the cache only ever holds checked files, and two
    # builds fetching at once don't meet.
    $part = Join-Path $cache "$($file.Name).$([Guid]::NewGuid().ToString('N')).part"
    # The waits before the second to the sixth try. The line between tries names no exception: MSBuild's Exec, which runs
    # this, takes an output line like "... returned an error: ..." for an error and fails the build even when a later
    # try succeeds.
    $waits = @(5, 10, 20, 30, 45)
    for ($try = 1; ; $try++) {
      try {
        Invoke-WebRequest -Uri $file.Url -OutFile $part -UserAgent $agent -UseBasicParsing
        break
      }
      catch {
        if ($try -gt $waits.Count) { throw "Couldn't download $($file.Name) from $($file.Url): $($_.Exception.Message)" }
        $wait = $waits[$try - 1]
        Write-Output "$($file.Name) not fetched (try $try of $($waits.Count + 1)), again in $wait s."
        Start-Sleep -Seconds $wait
      }
    }
    $hash = Get-Sha256 $part
    if ($hash -ne $file.Sha256) {
      Remove-Item $part -Force
      throw "$($file.Name) from $($file.Url) has the SHA-256 $hash, not the pinned $($file.Sha256): not kept."
    }
    Move-Item $part $kept -Force
  }
  $target = Join-Path $Out $file.Name
  Copy-Item $kept $target -Force
  if (-not (Test-Hash $target $file.Sha256)) { throw "$target isn't the pinned $($file.Name) after copying it." }
}
Write-Output "PaddleOCR's model ($revision) in $Out"
