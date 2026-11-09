<#
  الخطوة 1 — تجهيز جهاز المعمل (تشغيل مرة واحدة، PowerShell كـ Administrator)
  - يمنع النوم/الإسبات وإطفاء الهارد
  - يمنع ويندوز يعمل Restart لوحده بعد التحديثات
  - يظبط المنطقة الزمنية لمصر ومزامنة الساعة
  - يعمل فولدرات البرنامج والـ Backup
  - يتأكد إن SQL Server Express و Mosquitto متنصبين
#>
param(
    [string]$AppDir = 'C:\XChemicalStorage',
    [string]$BackupDir = 'D:\BioBank_Backups'
)
$ErrorActionPreference = 'Stop'
function Ok($m)   { Write-Host "  [OK]   $m" -ForegroundColor Green }
function Warn($m) { Write-Host "  [!!]   $m" -ForegroundColor Yellow; $script:warnings++ }
$warnings = 0

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'شغّلي PowerShell كـ Administrator (Run as administrator).'
}

Write-Host "`n== الطاقة ==" -ForegroundColor Cyan
powercfg /change standby-timeout-ac 0 | Out-Null
powercfg /change hibernate-timeout-ac 0 | Out-Null
powercfg /change disk-timeout-ac 0 | Out-Null
powercfg /hibernate off | Out-Null
Ok 'النوم والإسبات وإطفاء الهارد اتقفلوا (الشاشة ممكن تطفي عادي)'

Write-Host "`n== تحديثات ويندوز ==" -ForegroundColor Cyan
$au = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU'
New-Item -Path $au -Force | Out-Null
Set-ItemProperty -Path $au -Name NoAutoRebootWithLoggedOnUsers -Value 1 -Type DWord
Set-ItemProperty -Path $au -Name AUOptions -Value 3 -Type DWord      # تنزيل بس، والتنصيب يدوي
Ok 'ويندوز مش هيعمل Restart لوحده. خلي التحديثات يدوي في ميعاد صيانة متفق عليه'

Write-Host "`n== الوقت ==" -ForegroundColor Cyan
Set-TimeZone -Id 'Egypt Standard Time'
try {
    Set-Service w32time -StartupType Automatic; Start-Service w32time -ErrorAction SilentlyContinue
    w32tm /resync /force 2>$null | Out-Null
    Ok "المنطقة الزمنية مصر — الساعة دلوقتي $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
} catch { Warn 'مزامنة الساعة فشلت (مفيش نت؟). اتأكدي من الساعة يدوي' }

Write-Host "`n== الفولدرات ==" -ForegroundColor Cyan
foreach ($d in @($AppDir, $BackupDir)) {
    try { New-Item -ItemType Directory -Path $d -Force | Out-Null; Ok $d }
    catch { Warn "معرفتش أعمل $d (الدرايف موجود؟). غيّري -BackupDir" }
}
# SQL Server بيكتب الـ .bak بنفسه، فلازم حسابه يقدر يكتب في فولدر الـ Backup
try {
    $acl = Get-Acl $BackupDir
    $rule = New-Object Security.AccessControl.FileSystemAccessRule('NT SERVICE\MSSQL$SQLEXPRESS', 'Modify', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $acl.AddAccessRule($rule); Set-Acl $BackupDir $acl
    Ok 'صلاحية SQL Express على فولدر الـ Backup'
} catch { Warn 'معرفتش أدي SQL Express صلاحية على فولدر الـ Backup (هيتظبط بعد تنصيب SQL Express — شغلي السكريبت تاني)' }

Write-Host "`n== البرامج المطلوبة ==" -ForegroundColor Cyan
if (Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue) {
    Set-Service 'MSSQL$SQLEXPRESS' -StartupType Automatic; Ok 'SQL Server Express متنصب'
} else { Warn 'SQL Server Express مش متنصب → https://www.microsoft.com/sql-server/sql-server-downloads (Express, Basic)' }

if (Get-Service mosquitto -ErrorAction SilentlyContinue) {
    Set-Service mosquitto -StartupType Automatic; Ok 'Mosquitto متنصب'
} else { Warn 'Mosquitto مش متنصب → https://mosquitto.org/download (Windows 64-bit) — علّمي على Service' }

Write-Host ''
if ($warnings -eq 0) { Write-Host 'الجهاز جاهز. الخطوة الجاية: 2-configure-mosquitto.ps1' -ForegroundColor Green }
else { Write-Host "فيه $warnings ملاحظة فوق. صلّحيها وشغّلي السكريبت تاني." -ForegroundColor Yellow }
