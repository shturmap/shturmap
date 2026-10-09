# The Sentry DSN that reports go to, for eng\publish.ps1 and eng\release.ps1: SHTURMAP_SENTRY_DSN (the Release
# workflow sets it from the SENTRY_DSN secret), else the untracked eng\sentry.dsn (gitignored; never commit it). Prints
# the msbuild argument, or nothing: a build without it can't send reports (docs/DESIGN.md §8, "Reports").
$dsn = "$env:SHTURMAP_SENTRY_DSN".Trim()
$file = Join-Path $PSScriptRoot 'sentry.dsn'
if (-not $dsn -and (Test-Path $file)) { $dsn = (Get-Content $file -Raw).Trim() }
if ($dsn) { "-p:SentryDsn=$dsn" }
