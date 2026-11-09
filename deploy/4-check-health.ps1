<#
  الخطوة 4 — فحص شامل للنظام (شغّليه بعد التنصيب، وقبل ما تمشي من الموقع)
#>
param([int]$Port = 5000, [string]$BackupDir = 'D:\BioBank_Backups')
$fail = 0
function Check($name, [scriptblock]$test) {
    try { $r = & $test; if ($r) { Write-Host "  [OK]   $name" -ForegroundColor Green } else { throw 'x' } }
    catch { Write-Host "  [XX]   $name" -ForegroundColor Red; $script:fail++ }
}

Write-Host "`n== الخدمات ==" -ForegroundColor Cyan
Check 'SQL Server Express شغال'   { (Get-Service 'MSSQL$SQLEXPRESS').Status -eq 'Running' }
Check 'Mosquitto شغال'            { (Get-Service mosquitto).Status -eq 'Running' }
Check 'XChemicalStorage شغال'      { (Get-Service XChemicalStorage).Status -eq 'Running' }
Check 'الخدمة بتقوم مع الجهاز'     { (Get-CimInstance Win32_Service -Filter "Name='XChemicalStorage'").StartMode -eq 'Auto' }

Write-Host "`n== البرنامج ==" -ForegroundColor Cyan
$h = $null
try { $h = Invoke-RestMethod "http://localhost:$Port/healthz" -TimeoutSec 5 } catch { }
Check "الموقع بيرد على البورت $Port" { $null -ne $h }
Check 'حالة النظام ok (MQTT + الإنذارات + الإشعارات)' { $h.status -eq 'ok' }
if ($h -and $h.status -ne 'ok') { Write-Host "         الحالة: $($h.status) — التفاصيل من الأبلكيشن: IoT Health" -ForegroundColor Yellow }

Write-Host "`n== الجهاز ==" -ForegroundColor Cyan
Check 'النوم مقفول'                { "$((powercfg /query SCHEME_CURRENT SUB_SLEEP STANDBYIDLE) | Select-String 'Current AC Power Setting Index')" -match '0x00000000' }
Check 'المنطقة الزمنية مصر'        { (Get-TimeZone).Id -eq 'Egypt Standard Time' }
Check "فولدر الـ Backup موجود"      { Test-Path $BackupDir }
$last = Get-ChildItem $BackupDir -Filter *.bak -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
Check 'فيه Backup اتعمل'           { $null -ne $last }
if ($last) { Write-Host "         آخر Backup: $($last.Name)  ($([math]::Round($last.Length/1MB,1)) MB, $($last.LastWriteTime))" }
$ups = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue
if ($ups) { Write-Host '  [OK]   UPS متوصل بالـ USB' -ForegroundColor Green } else { Write-Host '  [--]   UPS مش متوصل بالـ USB (لو موجود ومتوصل كهربا بس، تمام)' -ForegroundColor Yellow }

Write-Host ''
if ($fail -eq 0) { Write-Host 'كله تمام.' -ForegroundColor Green } else { Write-Host "$fail فحص فشل. شوفي DEPLOYMENT.md (مشاكل شائعة)." -ForegroundColor Red }
