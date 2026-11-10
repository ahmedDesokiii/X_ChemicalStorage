<#
  الخطوة 3 — تنصيب/تحديث البرنامج كـ Windows Service
  - بيعمل publish (لو dotnet SDK موجود) أو ياخد فولدر publish جاهز
  - بينسخ لـ C:\XChemicalStorage ويحافظ على appsettings.Local.json
  - Service بيقوم مع الجهاز (Delayed) وبيعيد التشغيل لوحده لو وقع
  - بيفتح البورت 5000 على الشبكة المحلية

  تنصيب أول مرة أو تحديث:   .\3-install-service.ps1
  من فولدر publish جاهز:     .\3-install-service.ps1 -PublishDir D:\publish
#>
param(
    [string]$AppDir = 'C:\XChemicalStorage',
    [string]$PublishDir = '',
    [int]$Port = 5000
)
$ErrorActionPreference = 'Stop'
$svc = 'XChemicalStorage'
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root 'X_ChemicalStorage\X_ChemicalStorage.csproj'

if (-not $PublishDir) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet SDK مش موجود. اعملي Publish من Visual Studio ومرّري -PublishDir' }
    $PublishDir = Join-Path $PSScriptRoot 'out'
    Write-Host 'Publishing (Release, win-x64, self-contained)...' -ForegroundColor Cyan
    dotnet publish $proj -c Release -r win-x64 --self-contained true -o $PublishDir
    if ($LASTEXITCODE -ne 0) { throw 'الـ publish فشل' }
}
if (-not (Test-Path (Join-Path $PublishDir 'X_ChemicalStorage.exe'))) { throw "X_ChemicalStorage.exe مش موجود في $PublishDir" }

$existing = Get-Service $svc -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') {
    Write-Host 'إيقاف الخدمة الحالية...' -ForegroundColor Cyan
    Stop-Service $svc -Force
    (Get-Service $svc).WaitForStatus('Stopped', '00:00:30')
}

New-Item -ItemType Directory -Path $AppDir -Force | Out-Null
# نحافظ على الإعدادات والملفات المرفوعة الخاصة بالموقع
robocopy $PublishDir $AppDir /E /NFL /NDL /NJH /NJS /XF appsettings.Local.json /XD App_Data | Out-Null
$robocopyCode = $LASTEXITCODE
if ($robocopyCode -ge 8) { throw "النسخ فشل (robocopy code $robocopyCode)" }
New-Item -ItemType Directory -Path (Join-Path $AppDir 'App_Data') -Force | Out-Null

$local = Join-Path $AppDir 'appsettings.Local.json'
$prepared = Join-Path $PublishDir 'appsettings.Local.json'
if (-not (Test-Path $local) -and (Test-Path $prepared)) {
    # الباكدج المجهزة من make-site-package.ps1 فيها الإعدادات جاهزة
    Copy-Item $prepared $local
    Write-Host 'الإعدادات الجاهزة اتنسخت (appsettings.Local.json).' -ForegroundColor Green
}
if (-not (Test-Path $local)) {
    Copy-Item (Join-Path $AppDir 'appsettings.Local.example.json') $local
    Write-Host "`nاتعمل $local من النموذج. اكتبي باسورد الأدمن واحفظي، وبعدين شغّلي السكريبت تاني. الباقي من صفحة Settings في البرنامج." -ForegroundColor Yellow
    notepad $local
    return
}

$exe = Join-Path $AppDir 'X_ChemicalStorage.exe'
if (-not $existing) {
    $deps = @('mosquitto')
    if (Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue) { $deps += 'MSSQL$SQLEXPRESS' }
    New-Service -Name $svc -BinaryPathName "`"$exe`"" -DisplayName 'X Chemical Storage - BioBank' `
        -Description 'Chemical storage and Bio Bank freezer monitoring' -StartupType Automatic -DependsOn $deps | Out-Null
    sc.exe config $svc start= delayed-auto | Out-Null
    # إعادة تشغيل بعد 10 ثواني / 30 ثانية / دقيقة لو وقع، والعداد يتصفر كل يوم
    sc.exe failure $svc reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null
    sc.exe failureflag $svc 1 | Out-Null
    Write-Host 'الخدمة اتنصبت.' -ForegroundColor Green
}

# الخدمة شغالة كـ LocalSystem: لازم يكون ليها دخول على SQL Express (من غير sqlcmd)
try {
    $cn = New-Object System.Data.SqlClient.SqlConnection 'Server=.\SQLEXPRESS;Integrated Security=True;Connection Timeout=15'
    $cn.Open()
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = "IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'NT AUTHORITY\SYSTEM') CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS; IF IS_SRVROLEMEMBER('sysadmin', N'NT AUTHORITY\SYSTEM') = 0 ALTER SERVER ROLE sysadmin ADD MEMBER [NT AUTHORITY\SYSTEM];"
    [void]$cmd.ExecuteNonQuery(); $cn.Close()
    Write-Host 'صلاحية الخدمة على SQL Express تمام.' -ForegroundColor Green
} catch {
    Write-Host "معرفتش أوصل لـ SQL Express: $($_.Exception.Message)" -ForegroundColor Yellow
    Write-Host 'اتأكدي إن SQL Server Express متنصب وشغال (MSSQL$SQLEXPRESS).' -ForegroundColor Yellow
}

if (-not (Get-NetFirewallRule -DisplayName "BioBank Web $Port" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName "BioBank Web $Port" -Direction Inbound -Protocol TCP -LocalPort $Port -Profile Any -RemoteAddress LocalSubnet -Action Allow | Out-Null
}

# أول تشغيل بعد التنصيب/التحديث بيبني الداتابيز ويستورد site-setup.json، وده ممكن ياخد دقايق.
# Windows بيقفل أي خدمة تاخد أكتر من 30 ثانية تقوم، فبنشغله الأول كبرنامج عادي لحد ما يجهز.
Write-Host 'تشغيل أولي لتجهيز الداتابيز (ممكن ياخد لحد 10 دقايق أول مرة)...' -ForegroundColor Cyan
$warm = Start-Process -FilePath $exe -WorkingDirectory $AppDir -PassThru -WindowStyle Minimized
$ready = $false
foreach ($i in 1..200) {
    Start-Sleep 3
    if ($warm.HasExited) { break }
    try { Invoke-RestMethod "http://localhost:$Port/healthz" -TimeoutSec 5 | Out-Null; $ready = $true; break } catch { }
}
if (-not $warm.HasExited) { Stop-Process -Id $warm.Id -Force; Start-Sleep 2 }
if (-not $ready) {
    throw 'البرنامج مقامش في التشغيل الأولي. افتحي C:\XChemicalStorage وشغّلي X_ChemicalStorage.exe بإيدك وشوفي الخطأ.'
}
Write-Host '  [OK]   الداتابيز جاهزة.' -ForegroundColor Green

Start-Service $svc
Write-Host 'مستني البرنامج يقوم (أول مرة بيعمل الداتابيز)...' -ForegroundColor Cyan
$ok = $false
foreach ($i in 1..30) {
    Start-Sleep 3
    try { $r = Invoke-RestMethod "http://localhost:$Port/healthz" -TimeoutSec 5; $ok = $true; break } catch { }
}
if ($ok) {
    Write-Host "البرنامج شغال: http://localhost:$Port  (status = $($r.status))" -ForegroundColor Green
    Write-Host 'الفحص الحقيقي بيبدأ بعد دقيقتين من التشغيل. شغّلي 4-check-health.ps1 بعد 3 دقايق.'
} else {
    Write-Host 'البرنامج مقامش. افتحي Event Viewer > Windows Logs > Application وشوفي أخطاء XChemicalStorage.' -ForegroundColor Red
}
