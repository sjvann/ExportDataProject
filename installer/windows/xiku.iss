; 析庫 Windows 安裝程式。由 installer/build-installers.ps1 以 /D 傳入路徑與版本。

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#ifndef StageDir
  #define StageDir "..\..\artifacts\stage\win-x64"
#endif
#ifndef WebDir
  #define WebDir "..\..\artifacts\publish\win-x64\web"
#endif
#ifndef CliDir
  #define CliDir "..\..\artifacts\publish\win-x64\cli"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\artifacts\installers"
#endif
#ifndef IconFile
  #define IconFile "..\..\artifacts\installer-assets\icon.ico"
#endif

#define MyAppName "析庫"
#define MyAppPublisher "析庫"
#define MyAppURL "https://github.com/sjvann/ExportDataProject"

[Setup]
AppId={{8F4C2E1A-6B7D-4A93-9E15-2C8D5F0A1B37}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/releases
DefaultDirName={localappdata}\ExportData
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=ExportData-Setup-{#MyAppVersion}-win-x64
SetupIconFile={#IconFile}
UninstallDisplayIcon={app}\icon.ico
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion={#MyAppVersion}.0
VersionInfoProductName={#MyAppName}
VersionInfoDescription=本機舊系統資料庫工具
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes

[Languages]
Name: "chinesetraditional"; MessagesFile: "compiler:Languages\ChineseTraditional.isl"

[Messages]
chinesetraditional.WelcomeLabel2=這會在這台電腦安裝 [name/ver]。%n%n安裝檔已含執行環境。完成後從開始功能表開啟「析庫」，瀏覽器會前往 http://127.0.0.1:5107 。

[Tasks]
Name: "desktopicon"; Description: "建立桌面捷徑"; GroupDescription: "其他捷徑:"

[Files]
Source: "{#WebDir}\*"; DestDir: "{app}\web"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#CliDir}\*"; DestDir: "{app}\cli"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageDir}\start-workbench.cmd"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#StageDir}\使用說明.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#IconFile}"; DestDir: "{app}"; DestName: "icon.ico"; Flags: ignoreversion

[Icons]
Name: "{group}\析庫"; Filename: "{app}\start-workbench.cmd"; WorkingDir: "{app}"; IconFilename: "{app}\icon.ico"; Comment: "開啟析庫工作台"
Name: "{group}\析庫命令列"; Filename: "{cmd}"; Parameters: "/k ""{app}\cli\ExportData.exe"" --interactive"; WorkingDir: "{app}\cli"; IconFilename: "{app}\icon.ico"; Comment: "互動式匯出 CSV"
Name: "{group}\解除安裝析庫"; Filename: "{uninstallexe}"
Name: "{autodesktop}\析庫"; Filename: "{app}\start-workbench.cmd"; Tasks: desktopicon; WorkingDir: "{app}"; IconFilename: "{app}\icon.ico"

[Run]
Filename: "{app}\start-workbench.cmd"; Description: "啟動析庫"; Flags: postinstall nowait skipifsilent shellexec

[UninstallRun]
Filename: "{cmd}"; Parameters: "/c taskkill /F /IM ExportDataWeb.exe & taskkill /F /IM ExportData.exe & exit /b 0"; Flags: runhidden
