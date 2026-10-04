# Publishes the release eng\release.ps1 built (docs/DESIGN.md §8, "Distribution"): a GitHub pre-release
# "Shturmap <version> (private testing)", tag v<version> on the current commit, carrying Velopack's Setup, packages and
# update feed (vpk upload github), plus Shturmap-Setup.exe, the Setup under the name players look for. The release
# notes are docs\release-notes\<version>.md (packed into the release by eng\release.ps1).
# Refuses unless the working tree is clean, the commit is pushed, and artifacts\release was built from this commit.
# The token: GITHUB_TOKEN, else the GitHub CLI's (gh auth token), else Git's stored GitHub login (git credential).
# Usage: .\eng\publish-release.ps1 [-Draft]
param(
  # Leave the release as a draft on GitHub, to publish there by hand.
  [switch] $Draft
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$git = if (Get-Command git -ErrorAction SilentlyContinue) { 'git' } else { 'C:\Program Files\Git\cmd\git.exe' }
$version = (Select-Xml -Path (Join-Path $root 'Directory.Build.props') -XPath '//Version').Node.InnerText
$repo = 'https://github.com/shturmap/shturmap'
$api = 'https://api.github.com/repos/shturmap/shturmap'
$releases = Join-Path $root 'artifacts\release\packages'
$setup = Join-Path $root 'artifacts\release\Shturmap-Setup.exe'
$tag = "v$version"
$name = "Shturmap $version (private testing)"

# Clean and pushed: the tag must point at what is on GitHub, and the build must be that commit.
$dirty = & $git -C $root status --porcelain
if ($dirty) { throw "The working tree isn't clean:`n$($dirty -join "`n")" }
& $git -C $root fetch --quiet origin
$head = (& $git -C $root rev-parse HEAD).Trim()
if (-not (& $git -C $root branch -r --contains $head)) { throw "Commit $head isn't pushed; push it first." }
$built = (Get-Item (Join-Path $root 'artifacts\release\app\Shturmap.exe')).VersionInfo.ProductVersion
if ($built -ne "$version+$head") { throw "artifacts\release is from $built, not $version+${head}: run eng\release.ps1 again." }
# The version names the commit only: eng\release.ps1's note says whether the tree it built from was that commit's.
$note = Join-Path $root 'artifacts\release\built-from.txt'
$from = if (Test-Path $note) { (Get-Content $note -Raw).Trim() } else { 'unknown' }
if ($from -ne "$head clean") { throw "artifacts\release was built from '$from', not from a clean tree at ${head}: run eng\release.ps1 again." }
if (-not (Test-Path (Join-Path $releases "ShturmapApp-$version-full.nupkg")) -or -not (Test-Path $setup)) {
  throw 'No release in artifacts\release: run eng\release.ps1 first.'
}

$token = $env:GITHUB_TOKEN
if (-not $token -and (Get-Command gh -ErrorAction SilentlyContinue)) { $token = (& gh auth token).Trim() }
if (-not $token) {
  $login = "protocol=https`nhost=github.com`n`n" | & $git credential fill
  $token = ($login | Where-Object { $_ -like 'password=*' }) -replace '^password=', ''
}
if (-not $token) { throw 'No GitHub token: set GITHUB_TOKEN, or log in with gh or Git.' }

$dotnet = Join-Path $PSScriptRoot 'dotnet.ps1'
Push-Location $root
try {
  & $dotnet tool restore | Out-Null
  $publish = @(if (-not $Draft) { '--publish' })
  & $dotnet vpk upload github --repoUrl $repo --token $token --outputDir $releases --pre @publish `
    --releaseName $name --tag $tag --targetCommitish $head
  if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
  Pop-Location
}

# Shturmap-Setup.exe beside vpk's ShturmapApp-win-Setup.exe: the name the README and the notes give.
$headers = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github+json'; 'X-GitHub-Api-Version' = '2022-11-28' }
$release = (Invoke-RestMethod -Headers $headers "$api/releases?per_page=20") | Where-Object tag_name -eq $tag | Select-Object -First 1
if (-not $release) { throw "Release $tag not found on GitHub after the upload." }
$upload = $release.upload_url -replace '\{\?name,label\}$', '?name=Shturmap-Setup.exe'
Invoke-RestMethod -Method Post -Headers $headers -ContentType 'application/octet-stream' -InFile $setup $upload | Out-Null
Write-Output "$name $(if ($Draft) { 'is a draft' } else { 'is published' }): $($release.html_url)"
