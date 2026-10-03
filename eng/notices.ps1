# Gathers the licences a build must carry into <Out>\licenses: Shturmap's LICENSE, THIRD-PARTY-NOTICES.md, every
# licence and notice file the app's packages and the .NET runtime ship, and the texts some packages don't carry
# themselves (eng\licenses). The app project runs it before every publish and publishes <Out> beside the exe, so the
# installed app carries them in its folder (docs/DESIGN.md §3); the help panel's LICENCES link opens the folder.
# Usage: .\eng\notices.ps1 -Out <folder>
param([Parameter(Mandatory)] [string] $Out)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assets = Get-Content (Join-Path $root 'src\Shturmap.App\obj\project.assets.json') -Raw | ConvertFrom-Json
$cache = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$licenses = Join-Path $Out 'licenses'
if (Test-Path $licenses) { Remove-Item $licenses -Recurse -Force }
New-Item -ItemType Directory -Force $licenses | Out-Null

Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') $licenses
Copy-Item (Join-Path $PSScriptRoot 'licenses\*') $licenses

$isNotice = '(?i)^(licen[cs]e|notice|third-?party-?notices)[^/]*$'
$packages = @($assets.libraries.PSObject.Properties | Where-Object { $_.Value.type -eq 'package' } |
  ForEach-Object { [pscustomobject]@{ Name = $_.Name.Split('/')[0]; Path = $_.Value.path; Files = @($_.Value.files | Where-Object { $_ -match $isNotice }) } })
# The self-contained .NET runtime comes from a runtime pack, which the assets file lists as a download.
foreach ($framework in $assets.project.frameworks.PSObject.Properties) {
  foreach ($pack in $framework.Value.downloadDependencies | Where-Object { $_.name -like 'Microsoft.NETCore.App.Runtime.*' }) {
    $version = $pack.version.Trim('[', ']').Split(',')[0].Trim()
    $path = "$($pack.name.ToLowerInvariant())/$version"
    $files = @(Get-ChildItem (Join-Path $cache $path) -File | Where-Object { $_.Name -match $isNotice } | ForEach-Object Name)
    $packages += [pscustomobject]@{ Name = $pack.name; Path = $path; Files = $files }
  }
}
foreach ($package in $packages | Where-Object { $_.Files.Count -gt 0 }) {
  $target = New-Item -ItemType Directory -Force (Join-Path $licenses $package.Name)
  foreach ($file in $package.Files) { Copy-Item (Join-Path $cache (Join-Path $package.Path $file)) $target }
}
Write-Output "Licences and notices gathered in $licenses"
