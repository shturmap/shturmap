# Fails when a NuGet package the solution uses, directly or through another package, has a known vulnerability
# (nuget.org's advisories; docs/DESIGN.md §8, "Continuous integration"). CI runs it on every push, the Release workflow
# on the app before it builds. It stands in for Dependabot, whose pull requests would bring commits by another identity
# into a repository whose only one is shturmap (CLAUDE.md): a finding is fixed by hand, with a version raised in
# Directory.Packages.props.
# It fails on a finding, and when the packages couldn't be checked; "no vulnerable packages" passes.
# Usage: .\eng\audit-packages.ps1 [<solution or project>]   (the solution by default)
param(
  [string] $Target = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Shturmap.slnx')
)
$ErrorActionPreference = 'Stop'
$output = @(& (Join-Path $PSScriptRoot 'dotnet.ps1') list $Target package --vulnerable --include-transitive --format json)
$code = $LASTEXITCODE
# The JSON starts at its first line that opens an object; anything before it is the SDK talking.
$start = [Array]::FindIndex([string[]] $output, [Predicate[string]] { param($line) $line.StartsWith('{') })
if ($code -ne 0 -or $start -lt 0) {
  $output | Write-Output
  throw "dotnet list package failed (exit code $code): the packages weren't checked."
}
$report = ($output[$start..($output.Count - 1)] -join "`n") | ConvertFrom-Json

# Problems: an error means a project wasn't checked (no restore, no source), so it fails too; a warning is shown.
$errors = 0
foreach ($problem in @($report.problems | Where-Object { $_ })) {
  $where = if ($problem.project) { "$(Split-Path $problem.project -Leaf): " } else { '' }
  Write-Output "$($problem.level): $where$($problem.text)"
  if ($problem.level -eq 'error') { $errors++ }
}

$findings = foreach ($project in @($report.projects)) {
  foreach ($framework in @($project.frameworks | Where-Object { $_ })) {
    foreach ($kind in 'topLevelPackages', 'transitivePackages') {
      foreach ($package in @($framework.$kind | Where-Object { $_ })) {
        foreach ($vulnerability in @($package.vulnerabilities | Where-Object { $_ })) {
          '{0} ({1}): {2} {3}, {4}: {5} {6}' -f (Split-Path $project.path -Leaf), $framework.framework, $package.id,
            $package.resolvedVersion, $(if ($kind -eq 'topLevelPackages') { 'direct' } else { 'transitive' }),
            $vulnerability.severity, $vulnerability.advisoryurl
        }
      }
    }
  }
}
$findings = @($findings | Sort-Object -Unique)
$findings | Write-Output
$projects = @($report.projects).Count
if ($findings.Count -gt 0) { throw "$($findings.Count) known vulnerabilit$(if ($findings.Count -eq 1) { 'y' } else { 'ies' }) in the packages above: raise the package, or the one that brings it in, in Directory.Packages.props." }
if ($errors -gt 0) { throw "$errors project(s) couldn't be checked." }
if ($projects -eq 0) { throw 'No project was checked.' }
Write-Output "No known vulnerabilities in the packages of $projects project(s)."
