; Bộ cài đặt QLVB – Inno Setup 6. Không cần quyền quản trị, không kết nối Internet.
#define AppName "Quản lý văn bản đi – đến"
#define AppVersion "1.0.0"
#define AppExe "QLVB.exe"

[Setup]
AppId={{6C9E0F3A-2B7D-4E61-9F55-0A8C3D1E7B42}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=QLVB
AppCopyright=Phần mềm sử dụng nội bộ
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Bộ cài đặt {#AppName}
ShowLanguageDialog=no
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
; Bản dịch tiếng Việt không chính thức của Inno Setup (giữ nguyên ghi chú tác giả trong tệp), nạp sau Default.isl
; để mọi thông điệp còn thiếu (nếu có ở phiên bản Inno mới hơn) vẫn hiện bằng tiếng Anh thay vì lỗi biên dịch.
Name: "vi"; MessagesFile: "compiler:Default.isl,Languages\Vietnamese.isl"

[Messages]
vi.ConfirmUninstall=Gỡ cài đặt %1?%n%nDữ liệu, bản sao lưu và nhật ký trong thư mục AppData\Local\QLVB của tài khoản Windows KHÔNG bị xóa. Dữ liệu mật phải được xử lý theo quy định của cơ quan.
vi.UninstalledAll=Đã gỡ %1 khỏi máy tính.%n%nDữ liệu trong thư mục AppData\Local\QLVB vẫn được giữ nguyên.

[Tasks]
Name: "desktopicon"; Description: "Tạo biểu tượng trên màn hình nền (Desktop)"; GroupDescription: "Biểu tượng:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
; Thư viện gốc (SQLite mã hóa, WPF) nằm cạnh exe để không phải giải nén ra thư mục tạm khi chạy.
Source: "..\artifacts\publish\*.dll"; DestDir: "{app}"; Flags: ignoreversion
; Tài liệu HTML do tools/build-docs.py tạo từ docs/*.md (xem được ngoại tuyến).
Source: "..\artifacts\docs\*.html"; DestDir: "{app}\docs"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\NOTICE.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\CHANGELOG.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Mở phần mềm"; Flags: nowait postinstall skipifsilent

; Gỡ cài đặt KHÔNG xóa dữ liệu (%LOCALAPPDATA%\QLVB): dữ liệu mật phải được xử lý theo quy định, không tự động xóa.
