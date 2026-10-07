[Setup]
AppName=WebVirtualDisplayClient
AppVersion=1.0
DefaultDirName={autopf}\WebVirtualDisplayClient
DefaultGroupName=WebVirtualDisplayClient
UninstallDisplayIcon={app}\WebVirtualDisplayClient.exe
Compression=lzma2/max
SolidCompression=yes
OutputBaseFilename=WebVirtualDisplayClientSetup
PrivilegesRequired=lowest

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 1. Tell Inno Setup where your main executable is
Source: "bin\x64\Release\net8.0-windows\win-x64\publish\WebVirtualDisplayClient.exe"; DestDir: "{app}"; Flags: ignoreversion

; 2. Tell Inno Setup to grab all 400+ background DLLs and assets in that folder
Source: "bin\x64\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\WebVirtualDisplayClient"; Filename: "{app}\WebVirtualDisplayClient.exe"
Name: "{autodesktop}\WebVirtualDisplayClient"; Filename: "{app}\WebVirtualDisplayClient.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\WebVirtualDisplayClient.exe"; Description: "{cm:LaunchProgram,WebVirtualDisplayClient}"; Flags: nowait postinstall skipifsilent
