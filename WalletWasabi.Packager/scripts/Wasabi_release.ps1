# If you are not allowed to run this script, run the following command in your PowerShell console: 
# Set-ExecutionPolicy RemoteSigned

$host.UI.RawUI.ForegroundColor = "Green"
$host.UI.RawUI.BackgroundColor = "Black"
Read-Host -Prompt 'Releasing Wasabi Wallet - Insert a pendrive to store macOS notarization candidate files [Press ENTER]'
Read-Host -Prompt 'Start Kleopatra!'

# Making the paths user-independent
$basePath = Join-Path $env:USERPROFILE "Documents\GitHub\GingerWallet"
$walletWasabiPath = Join-Path $basePath "WalletWasabi.Packager"
$installerPath = Join-Path $basePath "WalletWasabi.WindowsInstaller\WalletWasabi.WindowsInstaller.wixproj"
$visualStudioPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.com"

# Change directory to the wallet wasabi packager folder
cd $walletWasabiPath
dotnet run -- publish

$host.UI.RawUI.ForegroundColor = "Green"
$host.UI.RawUI.BackgroundColor = "Black"
Read-Host -Prompt 'Remove and plug the pendrive to macOS and run the packager to notarize the files.'

# Arguments for Visual Studio
$arguments = "$installerPath /Build 'Release|x64'"

# Start Visual Studio with the specified arguments
Start-Process -FilePath $visualStudioPath -ArgumentList $arguments # If -Wait -NoNewWindow added devenv will hang forever at the end of the build.

$host.UI.RawUI.ForegroundColor = "Green"
$host.UI.RawUI.BackgroundColor = "Black"
Read-Host -Prompt 'Wait until WiX building the MSI installer, then [Press ENTER]'
Read-Host -Prompt 'Wait until macOS notarization is done and insert the pendrive to this PC [Press ENTER]'
dotnet run -- sign

Read-Host -Prompt 'Release finished [Press ENTER]'
