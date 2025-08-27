# Route git output to stdout
$env:GIT_REDIRECT_STDERR = '2>&1'

# --- Paths (relative to this script) ---
$RepoRoot  = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path          # ...\GingerWallet
$ParentDir = Split-Path $RepoRoot -Parent                                    # parent of GingerWallet
$CloneRoot = Join-Path $ParentDir 'GingerWalletClone'                        # sibling to GingerWallet
$WorkRoot  = Join-Path $ParentDir 'GingerDetWork'                            # sibling work area
$TempRoot  = Join-Path $WorkRoot 'temp'                                      # ...\GingerDetWork\temp
$OrigDist  = Join-Path $RepoRoot  'WalletWasabi.Fluent.Desktop\bin\dist'
$CdelRoot  = Join-Path $CloneRoot 'WalletWasabi.Fluent.Desktop\bin\dist\cdelivery'

# --- Clean work/clone (outside the repo) ---
if (Test-Path $WorkRoot)  { Remove-Item -Recurse -Force $WorkRoot }
if (Test-Path $CloneRoot) { Remove-Item -Recurse -Force $CloneRoot }
New-Item -ItemType Directory -Force -Path $TempRoot | Out-Null

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

# --- Expand clone artifacts to sibling temp (outside repo) ---
Get-ChildItem -Recurse -Path $CdelRoot -Filter "*linux-x64.zip"   | Select-Object -First 1 | ForEach-Object { Expand-Archive -Path $_.FullName -DestinationPath (Join-Path $TempRoot 'linux-x64') -Force }
Get-ChildItem -Recurse -Path $CdelRoot -Filter "*macOS-arm64.zip" | Select-Object -First 1 | ForEach-Object { Expand-Archive -Path $_.FullName -DestinationPath (Join-Path $TempRoot 'osx-arm64')  -Force }
Get-ChildItem -Recurse -Path $CdelRoot -Filter "*macOS-x64.zip"   | Select-Object -First 1 | ForEach-Object { Expand-Archive -Path $_.FullName -DestinationPath (Join-Path $TempRoot 'osx-x64')    -Force }
Get-ChildItem -Recurse -Path $CdelRoot -Filter "*win-x64.zip"     | Select-Object -First 1 | ForEach-Object { Expand-Archive -Path $_.FullName -DestinationPath (Join-Path $TempRoot 'win-x64')    -Force }

# (Optional) drop env metadata file if present so it doesn't affect determinism checks
Get-ChildItem $OrigDist -Recurse -Filter BUILDINFO.json -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem $TempRoot -Recurse -Filter BUILDINFO.json -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# --- Compare original dist (inside repo) vs expanded clone (outside repo) ---
$proc = Start-Process git -ArgumentList @('diff','--no-index','--exit-code', $OrigDist, $TempRoot) -NoNewWindow -PassThru -Wait

if ($proc.ExitCode -eq 0) {
  "Deterministic build succeed"
} else {
  "Deterministic build failed"
}
exit $proc.ExitCode
