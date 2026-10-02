# Puts the licences a build must carry next to it: LICENSE and THIRD-PARTY-NOTICES.md in the folder, and in
# licenses\ every licence and notice file the app's packages and the .NET runtime ship, plus the texts some
# packages don't carry themselves (eng\licenses). Run by publish.ps1 after a publish.
# Usage: .\eng\notices.ps1 -Out artifacts\Shturmap
param([Parameter(Mandatory)] [string] $Out)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assets = Get-Content (Join-Path $root 'src\Shturmap.App\obj\project.assets.json') -Raw | ConvertFrom-Json
$cache = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$licenses = Join-Path $Out 'licenses'
if (Test-Path $licenses) { Remove-Item $licenses -Recurse -Force }
New-Item -ItemType Directory -Force $licenses | Out-Null

Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') $Out
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
Write-Output "Licences and notices copied to $licenses"
