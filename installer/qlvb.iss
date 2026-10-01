; Bộ cài đặt QLVB – Inno Setup 6. Không cần quyền quản trị, không kết nối Internet.
#define AppName "Quản lý văn bản đi – đến"
#define AppVersion "1.0.0"
#define AppExe "QLVB.exe"

[Setup]
AppId={{6C9E0F3A-2B7D-4E61-9F55-0A8C3D1E7B42}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=QLVB
DefaultDirName={localappdata}\Programs\QLVB
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
OutputDir=..\artifacts\installer
OutputBaseFilename=QLVB-Setup-{#AppVersion}-win-x64
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
SetupIconFile=..\src\Qlvb.App\Assets\qlvb.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng trên màn hình nền (Desktop)"; GroupDescription: "Biểu tượng:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
; Thư viện gốc (SQLite mã hóa, WPF) nằm cạnh exe để không phải giải nén ra thư mục tạm khi chạy.
Source: "..\artifacts\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\*"; DestDir: "{app}\docs"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\NOTICE.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Mở phần mềm"; Flags: nowait postinstall skipifsilent

; Gỡ cài đặt KHÔNG xóa dữ liệu (%LOCALAPPDATA%\QLVB): dữ liệu mật phải được xử lý theo quy định, không tự động xóa.
