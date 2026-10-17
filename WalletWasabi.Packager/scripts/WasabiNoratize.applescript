-- Change only this path if the checkout is elsewhere.
set repositoryPath to (POSIX path of (path to home folder)) & "Desktop/GingerWallet"
set runnerPath to repositoryPath & "/WalletWasabi.Packager/scripts/sign-macos.sh"
tell application "Terminal"
    activate
    do script "/bin/bash " & quoted form of runnerPath
end tell
