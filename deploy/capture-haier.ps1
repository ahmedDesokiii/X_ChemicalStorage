<#
  تسجيل رسايل بوابات هاير لفترة، عشان نفك صيغة فريزر جديد (زي الـ -150)

  .\capture-haier.ps1                 # 20 دقيقة، كل البوابات
  .\capture-haier.ps1 -Minutes 10

  الملف بيتحفظ على سطح المكتب: haier-capture-<الوقت>.txt
  وهو شغال: صوّري شاشة كل فريزر ومعاها الساعة، وافتحي الباب 20 ثانية واكتبي الوقت.
#>
param([int]$Minutes = 20, [string]$Broker = '127.0.0.1')
$ErrorActionPreference = 'Stop'
$sub = 'C:\Program Files\mosquitto\mosquitto_sub.exe'
if (-not (Test-Path $sub)) { throw "مش لاقي $sub. Mosquitto متنصّب؟" }

$out = Join-Path ([Environment]::GetFolderPath('Desktop')) ("haier-capture-" + (Get-Date -Format 'yyyyMMdd_HHmm') + '.txt')
$end = (Get-Date).AddMinutes($Minutes)
Write-Host ''
Write-Host "  بيسجّل لحد الساعة $($end.ToString('HH:mm'))  ($Minutes دقيقة)" -ForegroundColor Cyan
Write-Host "  الملف: $out"
Write-Host '  متقفليش الشاشة دي. وهو شغال: صوّري الشاشات بالساعة، وافتحي الباب 20 ثانية.' -ForegroundColor Yellow
Write-Host ''

# -W = يقف لوحده بعد المدة. كل سطر: الوقت + الـ Topic + الرسالة hex
& $sub -h $Broker -t 'status/#' -F '%I %t %x' -W ($Minutes * 60) |
    Tee-Object -FilePath $out |
    ForEach-Object { $p = $_ -split ' '; if ($p.Count -ge 2) { Write-Host ("  " + $p[0].Substring(11, 8) + "  " + ($p[1] -split '/')[-1]) } }

$n = (Get-Content $out -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
Write-Host ''
Write-Host "  خلص: $n رسالة. ابعتي الملف ده مع الصور:" -ForegroundColor Green
Write-Host "  $out"
