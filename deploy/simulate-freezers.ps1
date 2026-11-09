<#
  محاكي فريزرات — بيبعت قراءات MQTT بنفس شكل البوابة الحقيقية، عشان تختبري الدورة كاملة
  (استقبال → حفظ → إنذار → إشعار → كشف الانقطاع) من غير فريزر حقيقي.

  أمثلة:
    .\simulate-freezers.ps1                                  # 3 فريزرات طبيعي كل 10 ثواني
    .\simulate-freezers.ps1 -Scenario HighTemp -Freezer SIM-80A
    .\simulate-freezers.ps1 -Scenario DoorOpen
    .\simulate-freezers.ps1 -Scenario PowerFail
    .\simulate-freezers.ps1 -Scenario Offline                # يبعت شوية وبعدين يسكت (اختبار الانقطاع)
    .\simulate-freezers.ps1 -User biobank -Password xxx      # لو الـ broker عليه باسورد

  شكل الرسالة:  topic = freezer/<CODE>/telemetry
  { "temperature": -79.8, "set_temp": -80, "ambient_temp": 24.5, "door": "closed", "power": "ok", "online": true }
#>
param(
    [ValidateSet('Normal', 'HighTemp', 'DoorOpen', 'PowerFail', 'Offline')]
    [string]$Scenario = 'Normal',
    [string]$Freezer = '',
    [string]$BrokerHost = '127.0.0.1',
    [int]$Port = 1883,
    [string]$User = '',
    [string]$Password = '',
    [int]$IntervalSeconds = 10,
    [int]$Count = 0   # 0 = لحد ما توقفيه بـ Ctrl+C
)

$ErrorActionPreference = 'Stop'

$pub = (Get-Command mosquitto_pub -ErrorAction SilentlyContinue).Source
if (-not $pub) { $pub = 'C:\Program Files\mosquitto\mosquitto_pub.exe' }
if (-not (Test-Path $pub)) { throw "mosquitto_pub مش موجود. نصّبي Mosquitto الأول (install-prerequisites.ps1)." }

# أكواد الفريزرات الوهمية ودرجة الضبط بتاعة كل واحد
$freezers = [ordered]@{ 'SIM-80A' = -80; 'SIM-20A' = -20; 'SIM-04A' = 4 }
if ($Freezer) {
    if (-not $freezers.Contains($Freezer)) { $freezers = [ordered]@{ $Freezer = -80 } }
    else { $freezers = [ordered]@{ $Freezer = $freezers[$Freezer] } }
}

$rand = [System.Random]::new()
$tmp = Join-Path $env:TEMP 'biobank-sim-payload.json'
$sent = 0
Write-Host "Scenario=$Scenario  Broker=${BrokerHost}:$Port  Freezers=$($freezers.Keys -join ', ')" -ForegroundColor Cyan

while ($true) {
    foreach ($code in $freezers.Keys) {
        $set = [double]$freezers[$code]
        $temp = $set + ($rand.NextDouble() - 0.5)          # تذبذب طبيعي ±0.5
        $door = 'closed'; $power = 'ok'

        switch ($Scenario) {
            'HighTemp'  { $temp = $set + 5 + ($sent * 1.5) }   # بتطلع تدريجياً لحد ما تعدّي الحد
            'DoorOpen'  { $door = 'open' }
            'PowerFail' { $power = 'fail'; $temp = $set + ($sent * 0.8) }
        }

        $payload = [ordered]@{
            temperature  = [math]::Round($temp, 1)
            set_temp     = $set
            ambient_temp = [math]::Round(24 + $rand.NextDouble(), 1)
            door         = $door
            power        = $power
            online       = $true
        } | ConvertTo-Json -Compress

        # الـ JSON بيتبعت من ملف: PowerShell 5.1 بيبوّظ علامات " لو اتبعتت كـ argument
        [System.IO.File]::WriteAllText($tmp, $payload, [System.Text.UTF8Encoding]::new($false))
        $pubArgs = @('-h', $BrokerHost, '-p', $Port, '-t', "freezer/$code/telemetry", '-f', $tmp, '-q', '1')
        if ($User) { $pubArgs += @('-u', $User, '-P', $Password) }
        & $pub @pubArgs
        if ($LASTEXITCODE -ne 0) { Write-Warning "فشل الإرسال لـ $code (الـ broker شغال؟ اليوزر والباسورد صح؟)" }
        else { Write-Host ("{0:HH:mm:ss}  {1,-8} {2}" -f (Get-Date), $code, $payload) }
    }

    $sent++
    if ($Scenario -eq 'Offline' -and $sent -ge 3) {
        Write-Host "`nOffline: وقفت الإرسال. المفروض إنذار انقطاع الاتصال يظهر خلال المهلة المضبوطة للفريزر." -ForegroundColor Yellow
        break
    }
    if ($Count -gt 0 -and $sent -ge $Count) { break }
    Start-Sleep -Seconds $IntervalSeconds
}
