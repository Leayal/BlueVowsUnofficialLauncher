echo off
dotnet publish ./BlueVowsLauncher/BlueVowsLauncher.csproj -c Release --no-self-contained -r win10-x86 /p:PublishReadyToRun=true -o ./Build/win10-x86
dotnet publish ./BlueVowsLauncher/BlueVowsLauncher.csproj -c Release --no-self-contained -r win10-x64 /p:PublishReadyToRun=true -o ./Build/win10-x64