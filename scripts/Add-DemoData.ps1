# Chạy tại repo đã pull code. Chỉ thêm dữ liệu vào schema 24 bảng có sẵn.
# Ví dụ: .\scripts\Add-DemoData.ps1 -Server localhost
[CmdletBinding()]
param(
    [ValidatePattern('^[^;\r\n]+$')]
    [string]$Server = 'localhost',
    [ValidatePattern('^[^;\r\n]+$')]
    [string]$Database = 'VeterinaryClinicDB'
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$seedProject = Join-Path $repoRoot 'tools/VCMS.DemoSeed/VCMS.DemoSeed.csproj'
$outputDirectory = Join-Path $repoRoot '.local-demo'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Cần cài .NET 8 SDK hoặc Visual Studio có workload ASP.NET.'
}
if (-not (Test-Path -LiteralPath $seedProject)) {
    throw 'Cần clone/pull đầy đủ repo; file PS1 sử dụng project tools/VCMS.DemoSeed đi kèm.'
}
$demoConnection = "Server=$Server;Database=$Database;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
& dotnet run --project $seedProject -- --connection $demoConnection --repo-root $repoRoot --output-dir $outputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Thêm dữ liệu demo chưa hoàn tất; xem lỗi phía trên. Script không reset dữ liệu cũ.' }
Write-Host "Tài khoản/mật khẩu: $(Join-Path $outputDirectory 'DEMO_ACCOUNTS.local.md')"
