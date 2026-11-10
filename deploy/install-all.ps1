<#
  تنصيب المعمل كله مرة واحدة — بيتشغّل من INSTALL.cmd (Run as administrator)
  كل خطوة بتتخطى لو كانت معمولة قبل كده، فينفع يتشغّل تاني بأمان.

  1) IP جهاز المعمل على شبكة البوابات (192.168.2.10)
  2) Mosquitto (من غير باسورد)          3) SQL Server Express
  4) تجهيز الجهاز (نوم، تحديثات، وقت، Backup)
  5) البرنامج كـ Windows Service + استيراد الفريزرات أول مرة
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dep  = Join-Path $root 'deploy'
$inst = Join-Path $root 'installers'
$app  = Join-Path $root 'app'
$log  = Join-Path $env:PUBLIC "BioBank-install-$(Get-Date -Format yyyyMMdd-HHmm).log"
Start-Transcript -Path $log | Out-Null

function Step($n, $t) { Write-Host "`n===== $n) $t =====" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "  [OK]   $m" -ForegroundColor Green }
function Warn($m) { Write-Host "  [!!]   $m" -ForegroundColor Yellow; $script:warnings += $m }
$warnings = @()

try {
    # ------------------------------------------------------------------ 1
    Step 1 'الشبكة'
    try { & (Join-Path $dep '0-set-network.ps1') }
    catch { Warn "الشبكة: $($_.Exception.Message)  ←  بعد ما توصّلي السويتش شغّلي deploy\0-set-network.ps1" }

    # ------------------------------------------------------------------ 2
    Step 2 'Mosquitto'
    if (-not (Get-Service mosquitto -ErrorAction SilentlyContinue)) {
        $m = Get-ChildItem $inst -Filter 'mosquitto*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $m) { throw "مفيش تنصيب Mosquitto في $inst" }
        Write-Host "  بنصّب $($m.Name)..."
        Start-Process $m.FullName -ArgumentList '/S' -Wait
        $mexe = 'C:\Program Files\mosquitto\mosquitto.exe'
        if (-not (Get-Service mosquitto -ErrorAction SilentlyContinue) -and (Test-Path $mexe)) { & $mexe install | Out-Null }
        if (-not (Get-Service mosquitto -ErrorAction SilentlyContinue)) { throw 'Mosquitto اتنصب بس الخدمة مش موجودة. نصّبيه بإيدك وعلّمي على Service.' }
    }
    Ok 'Mosquitto متنصب'
    & (Join-Path $dep '2-configure-mosquitto.ps1')

    # ------------------------------------------------------------------ 3
    Step 3 'SQL Server Express'
    if (-not (Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue)) {
        $sq = Get-ChildItem $inst -Filter 'SQLEXPR*.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $sq) { throw "مفيش SQLEXPR_x64_ENU.exe في $inst" }
        $x = Join-Path $env:TEMP 'SQLEXPR_setup'
        Write-Host '  بفك ملفات التنصيب...'
        Start-Process $sq.FullName -ArgumentList "/q /x:`"$x`"" -Wait
        $setupExe = Join-Path $x 'SETUP.EXE'
        foreach ($i in 1..60) { if (Test-Path $setupExe) { break }; Start-Sleep 2 }
        Start-Sleep 5
        if (-not (Test-Path $setupExe)) { throw 'فك ملفات SQL Express فشل. نصّبيه بإيدك (Basic، اسم SQLEXPRESS).' }
        Write-Host '  بنصّب SQL Server Express (5–15 دقيقة، متقفليش الشاشة)...'
        $sqlArgs = '/Q /ACTION=Install /FEATURES=SQLEngine /INSTANCENAME=SQLEXPRESS /IACCEPTSQLSERVERLICENSETERMS ' +
                '/UPDATEENABLED=0 /SQLSYSADMINACCOUNTS="BUILTIN\Administrators" "NT AUTHORITY\SYSTEM"'
        $p = Start-Process $setupExe -ArgumentList $sqlArgs -Wait -PassThru
        if (-not (Get-Service 'MSSQL$SQLEXPRESS' -ErrorAction SilentlyContinue)) {
            throw "تنصيب SQL Express فشل (code $($p.ExitCode)). اللوج: C:\Program Files\Microsoft SQL Server\*\Setup Bootstrap\Log"
        }
    }
    Ok 'SQL Server Express متنصب'

    # ------------------------------------------------------------------ 4
    Step 4 'تجهيز الجهاز'
    $d = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='D:' AND DriveType=3" -ErrorAction SilentlyContinue
    $backupDir = if ($d) { 'D:\BioBank_Backups' } else { 'C:\BioBank_Backups' }
    & (Join-Path $dep '1-prepare-machine.ps1') -BackupDir $backupDir

    # مكان الـ Backup في إعدادات الموقع
    $localPath = Join-Path $app 'appsettings.Local.json'
    if (Test-Path $localPath) {
        $cfg = Get-Content $localPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $cfg | Add-Member -NotePropertyName Backup -NotePropertyValue ([pscustomobject]@{ Directory = $backupDir }) -Force
        [IO.File]::WriteAllText($localPath, ($cfg | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
        Ok "الـ Backup اليومي ← $backupDir"
    }

    # ------------------------------------------------------------------ 5
    Step 5 'البرنامج'
    & (Join-Path $dep '3-install-service.ps1') -PublishDir $app

    Write-Host "`n=========================================" -ForegroundColor Green
    Write-Host ' خلص. افتحي: http://localhost:5000' -ForegroundColor Green
    Write-Host ' الخطوة الجاية: ظبط البوابات (DEPLOYMENT.md - 2.5)' -ForegroundColor Green
    Write-Host "=========================================" -ForegroundColor Green
    if ($warnings.Count) { Write-Host "`nملاحظات:" -ForegroundColor Yellow; $warnings | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow } }
    Start-Process 'http://localhost:5000'
}
catch {
    Write-Host "`n[XX] وقف هنا: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "صلّحي اللي فوق وشغّلي INSTALL.cmd تاني، الخطوات اللي خلصت هتتخطى." -ForegroundColor Red
}
finally {
    Stop-Transcript | Out-Null
    Write-Host "اللوج: $log"
}
