# Deterministic compare: run from ...\GingerWallet\WalletWasabi.Packager\scripts
# Nothing is written inside the repo; all work happens next to it.

$ErrorActionPreference = 'Stop'
$env:GIT_REDIRECT_STDERR = '2>&1'

# --- Paths (relative to this script) ---
$RepoRoot  = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path          # ...\GingerWallet
$ParentDir = Split-Path $RepoRoot -Parent                                    # parent of GingerWallet
$CloneRoot = Join-Path $ParentDir 'GingerWalletClone'                        # sibling to GingerWallet
$WorkRoot  = Join-Path $ParentDir 'GingerDetWork'                            # sibling work area
$TempRoot  = Join-Path $WorkRoot 'temp'                                      # ...\GingerDetWork\temp
$OrigDist  = Join-Path $RepoRoot  'WalletWasabi.Fluent.Desktop\bin\dist'
$CdelRoot  = Join-Path $CloneRoot 'WalletWasabi.Fluent.Desktop\bin\dist\cdelivery'
$Constants = Join-Path $RepoRoot 'WalletWasabi\Helpers\Constants.cs'

# --- Helpers ---
function Expand-ZipToRid {
  param(
    [Parameter(Mandatory=$true)][string[]]$Patterns,  # patterns to try in order
    [Parameter(Mandatory=$true)][string]$ZipRoot,     # cdelivery path
    [Parameter(Mandatory=$true)][string]$DestRoot,    # $TempRoot\<rid>
    [string]$RidLabel                                  # for messages
  )
  $zip = $null
  foreach ($p in $Patterns) {
    $cand = Get-ChildItem -Recurse -Path $ZipRoot -Filter $p | Select-Object -First 1
    if ($cand) { $zip = $cand; break }
  }
  if (-not $zip) {
    Write-Warning "No archive found for $RidLabel matching: $($Patterns -join ' | ') in '$ZipRoot'"
    return $false
  }

  # Extract to a throwaway dir, then flatten if the zip has a single top-level folder
  $tmpExtract = Join-Path $WorkRoot ("_x_" + [System.IO.Path]::GetFileNameWithoutExtension($zip.Name))
  if (Test-Path $tmpExtract) { Remove-Item -Recurse -Force $tmpExtract }
  New-Item -ItemType Directory -Force -Path $tmpExtract | Out-Null

  Expand-Archive -Path $zip.FullName -DestinationPath $tmpExtract -Force

  $top = Get-ChildItem -LiteralPath $tmpExtract -Force
  $inner = if ($top.Count -eq 1 -and $top[0].PSIsContainer) { $top[0].FullName } else { $tmpExtract }

  $dest = Join-Path $TempRoot $DestRoot
  if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
  New-Item -ItemType Directory -Force -Path $dest | Out-Null

  Copy-Item -Path (Join-Path $inner '*') -Destination $dest -Recurse -Force
  Remove-Item -Recurse -Force $tmpExtract
  return $true
}

# --- Clean work/clone (outside the repo) ---
if (Test-Path $WorkRoot)  { Remove-Item -Recurse -Force $WorkRoot }
if (Test-Path $CloneRoot) { Remove-Item -Recurse -Force $CloneRoot }
New-Item -ItemType Directory -Force -Path $TempRoot | Out-Null

# --- Parse 4-part version from Constants.ClientVersion and export to env ---
$verMatch = Select-String -Path $Constants -Pattern 'ClientVersion\s*=\s*new\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)' | Select-Object -First 1
if (-not $verMatch) { throw "Could not find ClientVersion in $Constants" }
$env:VersionMajor    = $verMatch.Matches[0].Groups[1].Value
$env:VersionMinor    = $verMatch.Matches[0].Groups[2].Value
$env:VersionPatch    = $verMatch.Matches[0].Groups[3].Value
$env:VersionRevision = $verMatch.Matches[0].Groups[4].Value

# Commit for both builds (same repo revision)
$env:CommitHash = (git -C $RepoRoot rev-parse --short=12 HEAD).Trim()

# Determinism-friendly compiler flags
$env:ContinuousIntegrationBuild = 'true'
$env:DeterministicSourcePaths   = 'true'

# --- Copy source to sibling clone (not inside repo) ---
robocopy $RepoRoot $CloneRoot /COPYALL /S /NFL /NDL /NJH | Out-Null
$LASTEXITCODE = 0
# Keep git metadata in the clone so both builds see the same commit info
if (Test-Path (Join-Path $RepoRoot '.git')) {
  robocopy (Join-Path $RepoRoot '.git') (Join-Path $CloneRoot '.git') /MIR /NFL /NDL /NJH | Out-Null
  $LASTEXITCODE = 0
}

# --- Build original & clone ---
dotnet run --project (Join-Path $RepoRoot  'WalletWasabi.Packager') --onlybinaries
dotnet run --project (Join-Path $CloneRoot 'WalletWasabi.Packager') --cdelivery

# --- Expand clone artifacts to sibling temp (outside repo), normalizing layout ---
$ok = $true
$ok = (Expand-ZipToRid -Patterns @('*linux-x64.zip')             -ZipRoot $CdelRoot -DestRoot 'linux-x64' -RidLabel 'linux-x64') -and $ok
$ok = (Expand-ZipToRid -Patterns @('*macOS-arm64.zip','*osx-arm64.zip') -ZipRoot $CdelRoot -DestRoot 'osx-arm64' -RidLabel 'osx-arm64') -and $ok
$ok = (Expand-ZipToRid -Patterns @('*macOS-x64.zip','*osx-x64.zip')     -ZipRoot $CdelRoot -DestRoot 'osx-x64'   -RidLabel 'osx-x64')   -and $ok
$ok = (Expand-ZipToRid -Patterns @('*win-x64.zip','*win7-x64.zip')      -ZipRoot $CdelRoot -DestRoot 'win-x64'   -RidLabel 'win-x64')   -and $ok
if (-not $ok) { throw "One or more expected artifacts were not found under '$CdelRoot'." }

# Remove environment-bearing metadata so it doesn't affect determinism checks
Get-ChildItem $OrigDist -Recurse -Filter BUILDINFO.json -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem $TempRoot -Recurse -Filter BUILDINFO.json -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# --- Compare original dist (inside repo) vs expanded clone (outside repo) ---
$proc = Start-Process git -ArgumentList @('diff','--no-index','--exit-code', $OrigDist, $TempRoot) -NoNewWindow -PassThru -Wait

if ($proc.ExitCode -eq 0) {
  Write-Output "Deterministic build succeed"
} else {
  Write-Output "Deterministic build failed"
}
exit $proc.ExitCode
