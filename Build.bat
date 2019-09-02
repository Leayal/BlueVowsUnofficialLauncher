echo off
dotnet publish ./BlueVowsLauncher/BlueVowsLauncher.csproj -c Release --self-contained -r win10-x86 /p:PublishReadyToRun=true -o ./Build/Full/win10-x86
dotnet publish ./BlueVowsLauncher/BlueVowsLauncher.csproj -c Release --self-contained -r win7-x86 /p:PublishReadyToRun=true -o ./Build/Full/win7-x86

dotnet publish ./BlueVowsLauncher/BlueVowsLauncher.csproj -c Release --no-self-contained -r win10-x86 /p:PublishReadyToRun=true -o ./Build/Compact/win10-x86
dotnet publish ./BlueVowsLauncher/BlueVowsLauncher.csproj -c Release --no-self-contained -r win10-x64 /p:PublishReadyToRun=true -o ./Build/Compact/win10-x64
dotnet publish ./BlueVowsLauncher/BlueVowsLauncher.csproj -c Release --no-self-contained -r win7-x86 /p:PublishReadyToRun=true -o ./Build/Compact/win7-x86

REM dotnet publish ./BlueVowsLauncher/BlueVowsLauncher.csproj -c Release --self-contained -r win10-x64 /p:PublishReadyToRun=true -o ./Build/win10-x64