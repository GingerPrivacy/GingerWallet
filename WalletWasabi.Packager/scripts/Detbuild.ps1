# Route git output to stdout
$env:GIT_REDIRECT_STDERR = '2>&1'


# Clean temp
if (Test-Path temp) { Remove-Item -Recurse -Force temp }
New-Item -ItemType Directory -Force -Path temp | Out-Null

# Copy source and build
robocopy GingerWallet GingerWalletClone /COPYALL /S /NFL /NDL /NJH | Out-Null
$LASTEXITCODE = 0

dotnet run --project GingerWallet\WalletWasabi.Packager --onlybinaries
dotnet run --project GingerWalletClone\WalletWasabi.Packager --cdelivery

# Expand new artifact names
Get-ChildItem -Recurse -Path GingerWalletClone\WalletWasabi.Fluent.Desktop\bin\dist\cdelivery -Filter "*linux-x64.zip"   | Select-Object -First 1 | ForEach-Object { Expand-Archive -Path $_.FullName -DestinationPath temp\linux-x64   -Force }
Get-ChildItem -Recurse -Path GingerWalletClone\WalletWasabi.Fluent.Desktop\bin\dist\cdelivery -Filter "*macOS-arm64.zip" | Select-Object -First 1 | ForEach-Object { Expand-Archive -Path $_.FullName -DestinationPath temp\osx-arm64 -Force }
Get-ChildItem -Recurse -Path GingerWalletClone\WalletWasabi.Fluent.Desktop\bin\dist\cdelivery -Filter "*macOS-x64.zip"   | Select-Object -First 1 | ForEach-Object { Expand-Archive -Path $_.FullName -DestinationPath temp\osx-x64   -Force }
Get-ChildItem -Recurse -Path GingerWalletClone\WalletWasabi.Fluent.Desktop\bin\dist\cdelivery -Filter "*win-x64.zip"     | Select-Object -First 1 | ForEach-Object { Expand-Archive -Path $_.FullName -DestinationPath temp\win-x64     -Force }

# Compare original dist vs expanded clone (robust exit code)
$proc = Start-Process git -ArgumentList @(
  'diff','--no-index','--exit-code',
  'GingerWallet\WalletWasabi.Fluent.Desktop\bin\dist','temp'
) -NoNewWindow -PassThru -Wait

if ($proc.ExitCode -eq 0) {
  Write-Output "Deterministic build succeed"
} else {
  Write-Output "Deterministic build failed"
}
exit $proc.ExitCode