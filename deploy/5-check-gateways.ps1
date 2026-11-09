<#
  الخطوة 5 — فحص بوابات هاير (InHand IG502) على الشبكة المحلية
  لكل بوابة في gateways.csv:
    - هل بترد على الشبكة (ping)
    - هل بتبعت رسايل MQTT للجهاز ده، وآخر رسالة شكلها إيه
    - هل الحرارة قريبة من المتوقع

  مثال:  .\5-check-gateways.ps1                                   (Mosquitto من غير باسورد)
         .\5-check-gateways.ps1 -User biobank -Password '...'      (لو عليه باسورد)
         .\5-check-gateways.ps1 -ListenSeconds 90
#>
param(
    [string]$User = '',
    [string]$Password = '',
    [int]$ListenSeconds = 60,
    [string]$CsvPath = (Join-Path $PSScriptRoot 'gateways.csv'),
    [string]$MosquittoDir = 'C:\Program Files\mosquitto'
)
$ErrorActionPreference = 'Stop'
$sub = Join-Path $MosquittoDir 'mosquitto_sub.exe'
if (-not (Test-Path $sub)) { throw "mosquitto_sub مش موجود في $MosquittoDir" }
$gateways = Import-Csv $CsvPath

Write-Host "`n== الشبكة ==" -ForegroundColor Cyan
$myIps = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -like '192.168.2.*' }).IPAddress
if ($myIps) { Write-Host "  [OK]   IP الجهاز على شبكة البوابات: $($myIps -join ', ')" -ForegroundColor Green }
else { Write-Host '  [XX]   الجهاز مش على شبكة 192.168.2.x — ظبطي IP ثابت 192.168.2.10' -ForegroundColor Red }

# بوابة لسه على الـ IP الافتراضي = لسه متظبطتش (وممكن تعمل تعارض)
if (Test-Connection 192.168.2.1 -Count 1 -Quiet) {
    Write-Host '  [!!]   فيه جهاز على 192.168.2.1 (IP البوابة الافتراضي) — فيه بوابة لسه متظبطتش' -ForegroundColor Yellow
}

foreach ($g in $gateways) {
    $g | Add-Member -NotePropertyName Ping -NotePropertyValue (Test-Connection $g.GatewayIP -Count 2 -Quiet)
}

Write-Host "`n== بسمع على MQTT لمدة $ListenSeconds ثانية ==" -ForegroundColor Cyan
$subArgs = @('-h', '127.0.0.1', '-t', 'freezer/#', '-v', '-W', $ListenSeconds)
if ($User) { $subArgs += @('-u', $User, '-P', $Password) }
$out = & $sub @subArgs 2>$null
$last = @{}; $count = @{}
foreach ($line in @($out)) {
    if ($line -match '^freezer/([^/]+)/\S+\s+(.*)$') {
        $code = $Matches[1]; $last[$code] = $Matches[2]
        $count[$code] = 1 + ($count[$code] -as [int])
    }
}

Write-Host ''
$fail = 0
foreach ($g in $gateways) {
    $msgs = [int]$count[$g.Collector]
    $temp = $null
    if ($last[$g.Collector]) {
        try {
            $j = $last[$g.Collector] | ConvertFrom-Json
            foreach ($k in 'temperature', 'temp', 'current_temp', 'tem') { if ($null -ne $j.$k) { $temp = [double]$j.$k; break } }
        } catch { }
    }
    $state = if (-not $g.Ping) { 'XX'; $fail++ } elseif ($msgs -eq 0) { '!!'; $fail++ } else { 'OK' }
    $color = @{ OK = 'Green'; '!!' = 'Yellow'; XX = 'Red' }[$state]
    $tTxt = if ($null -ne $temp) { "$temp°C" } else { '-' }
    Write-Host ("  [{0}]   {1,-10} {2}  {3,-14} ping={4,-5} رسايل={5,-3} حرارة={6}" -f $state, $g.Name, $g.Logger, $g.GatewayIP, $g.Ping, $msgs, $tTxt) -ForegroundColor $color
    if ($null -ne $temp -and [math]::Abs($temp - [double]$g.ExpectedTemp) -gt 15) {
        Write-Host "           الحرارة بعيدة عن المتوقع ($($g.ExpectedTemp)°C) — راجعي الفريزر أو الـ TemperatureScale" -ForegroundColor Yellow
    }
    if ($state -eq '!!' ) { Write-Host '           البوابة على الشبكة بس مش بتبعت — راجعي إعدادات MQTT فيها (IP/Port/User/Topic)' -ForegroundColor Yellow }
    if ($state -eq 'XX' ) { Write-Host '           البوابة مش على الشبكة — الكابل؟ الـ IP اتغير صح؟' -ForegroundColor Red }
}

$unknown = $last.Keys | Where-Object { $_ -notin $gateways.Collector }
foreach ($u in $unknown) { Write-Host "  [??]   كود مش في gateways.csv بيبعت: $u  ←  $($last[$u])" -ForegroundColor Yellow }

$sample = $last.Values | Select-Object -First 1
if ($sample) { Write-Host "`nشكل رسالة حقيقية (للتأكد من أسماء الحقول):`n  $sample" -ForegroundColor Cyan }

Write-Host ''
if ($fail -eq 0) { Write-Host 'كل البوابات شغالة وبتبعت.' -ForegroundColor Green } else { Write-Host "$fail بوابة محتاجة مراجعة." -ForegroundColor Red }
