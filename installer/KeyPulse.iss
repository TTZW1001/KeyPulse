#define MyAppName "KeyPulse"
#define MyAppVersion "1.1.4"
#define MyAppPublisher "TTZW1001"
#define MyAppExeName "KeyPulse.exe"

[Setup]
AppId={{C6BBFD9D-774B-4B23-8A78-94F56B5D1B42}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\release
OutputBaseFilename=KeyPulse-Setup-x64
SetupIconFile=..\assets\keypulse.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} installer

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[CustomMessages]
english.UpgradePrompt=KeyPulse %1 is installed.%n%nUpgrade to %2 now? Your statistics, settings, and exclusions will be preserved.%n%nPlease exit KeyPulse from the system tray before continuing.
english.RepairPrompt=KeyPulse %1 is already installed.%n%nRepair or overwrite this installation? Your statistics, settings, and exclusions will be preserved.%n%nPlease exit KeyPulse from the system tray before continuing.
english.DowngradeBlocked=A newer KeyPulse version (%1) is installed. This installer (%2) will not downgrade it. Uninstall the newer version first if you intentionally need to downgrade.
chinesesimplified.UpgradePrompt=检测到已安装 KeyPulse %1。%n%n是否升级到 %2？统计数据、设置和排除列表都会保留。%n%n继续前请先从系统托盘退出 KeyPulse。
chinesesimplified.RepairPrompt=检测到已安装相同版本 KeyPulse %1。%n%n是否修复或覆盖安装？统计数据、设置和排除列表都会保留。%n%n继续前请先从系统托盘退出 KeyPulse。
chinesesimplified.DowngradeBlocked=检测到更高版本的 KeyPulse（%1）。此安装器（%2）不会执行降级。如确需降级，请先卸载较新版本。

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function NextVersionPart(var Version: String): Integer;
var
  P: Integer;
  Part: String;
begin
  P := Pos('.', Version);
  if P = 0 then
  begin
    Part := Version;
    Version := '';
  end
  else
  begin
    Part := Copy(Version, 1, P - 1);
    Delete(Version, 1, P);
  end;
  Result := StrToIntDef(Part, 0);
end;

function CompareVersions(LeftVersion, RightVersion: String): Integer;
var
  I, LeftPart, RightPart: Integer;
begin
  Result := 0;
  for I := 1 to 4 do
  begin
    LeftPart := NextVersionPart(LeftVersion);
    RightPart := NextVersionPart(RightVersion);
    if LeftPart < RightPart then
    begin
      Result := -1;
      Exit;
    end;
    if LeftPart > RightPart then
    begin
      Result := 1;
      Exit;
    end;
  end;
end;

function GetInstalledVersion(var Version: String): Boolean;
var
  UninstallKey: String;
begin
  UninstallKey := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{C6BBFD9D-774B-4B23-8A78-94F56B5D1B42}_is1';
  Result := RegQueryStringValue(HKCU64, UninstallKey, 'DisplayVersion', Version);
  if not Result then
    Result := RegQueryStringValue(HKCU32, UninstallKey, 'DisplayVersion', Version);
end;

function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
  Comparison: Integer;
begin
  Result := True;
  if not GetInstalledVersion(InstalledVersion) then
    Exit;

  Comparison := CompareVersions(InstalledVersion, '{#MyAppVersion}');
  if Comparison > 0 then
  begin
    if not WizardSilent then
      MsgBox(FmtMessage(CustomMessage('DowngradeBlocked'), [InstalledVersion, '{#MyAppVersion}']), mbError, MB_OK);
    Result := False;
  end
  else if not WizardSilent then
  begin
    if Comparison < 0 then
      Result := MsgBox(FmtMessage(CustomMessage('UpgradePrompt'), [InstalledVersion, '{#MyAppVersion}']),
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES
    else
      Result := MsgBox(FmtMessage(CustomMessage('RepairPrompt'), [InstalledVersion]),
        mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
  end;
end;
