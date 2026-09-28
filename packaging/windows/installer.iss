; Instalador do XSD Visualizer (Inno Setup 6).
; Uso: iscc /DAppVersion=1.2.0 /DArch=x64 /DSourceDir=<publish> /DOutputDir=<saída> installer.iss

#define AppName "XSD Visualizer"
#define AppExe "XsdVisualizer.exe"

[Setup]
AppId={{6B3F1C52-8E4A-4D1B-9C2E-5A7D0F3B8E41}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Renato Assis
AppPublisherURL=https://github.com/renatoassis01/xsd-visualizer
AppSupportURL=https://github.com/renatoassis01/xsd-visualizer/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\..\src\XsdVisualizer.App\Assets\app.ico
LicenseFile=..\..\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=XsdVisualizer-{#AppVersion}-windows-{#Arch}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Instala só para o usuário, sem pedir administrador; quem quiser pode instalar para todos.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
