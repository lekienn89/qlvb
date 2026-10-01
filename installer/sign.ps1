# Ký số tệp bằng chứng thư ký mã (code signing) của cơ quan.
# Chứng thư lấy từ biến môi trường (GitHub Actions secrets), KHÔNG để trong kho mã:
#   SIGN_PFX_BASE64    nội dung tệp .pfx mã hóa base64
#   SIGN_PFX_PASSWORD  mật khẩu tệp .pfx
#   SIGN_TIMESTAMP_URL máy chủ gắn dấu thời gian (tùy chọn; mặc định của DigiCert)
# Không có chứng thư thì bỏ qua (bản dựng chưa ký), không báo lỗi.
param([Parameter(Mandatory = $true)][string[]]$Files)

if (-not $env:SIGN_PFX_BASE64) {
    Write-Host "Chưa cấu hình chứng thư ký số (SIGN_PFX_BASE64): bỏ qua bước ký."
    exit 0
}
$signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName | Select-Object -Last 1
if (-not $signtool) { Write-Error "Không tìm thấy signtool.exe"; exit 1 }
$ts = if ($env:SIGN_TIMESTAMP_URL) { $env:SIGN_TIMESTAMP_URL } else { "http://timestamp.digicert.com" }
$pfx = Join-Path $env:RUNNER_TEMP ("sign-" + [guid]::NewGuid().ToString("N") + ".pfx")
try {
    [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:SIGN_PFX_BASE64))
    foreach ($f in $Files) {
        foreach ($item in Get-ChildItem $f) {
            & $signtool.FullName sign /fd SHA256 /f $pfx /p $env:SIGN_PFX_PASSWORD /tr $ts /td SHA256 /d "Quản lý văn bản đi – đến" $item.FullName
            if ($LASTEXITCODE -ne 0) { Write-Error "Ký số thất bại: $($item.Name)"; exit 1 }
            & $signtool.FullName verify /pa $item.FullName
            if ($LASTEXITCODE -ne 0) { Write-Error "Chữ ký không hợp lệ: $($item.Name)"; exit 1 }
        }
    }
}
finally {
    if (Test-Path $pfx) { Remove-Item $pfx -Force }
}
