<#
  تجهيز فلاشة/هارد الموقع — على جهازك، قبل ما تروحي

  .\make-site-package.ps1 -Target E:\BioBank_Site

  بيعمل في الفولدر ده:
    INSTALL.cmd                 ← الملف الوحيد اللي هتشغّليه في المعمل (Run as administrator)
    app\                        ← البرنامج (Publish، self-contained، مش محتاج .NET على جهاز المعمل)
    app\appsettings.Local.json  ← باسورد الأدمن (الحاجة الوحيدة اللي ليها باسورد)
    app\site-setup.json         ← الفريزرات والربط والحدود وجهات الاتصال (من Settings ← System)
    deploy\                     ← السكريبتات
    installers\                 ← هنا تحطي تنصيب SQL Server Express و Mosquitto
#>
param(
    [Parameter(Mandatory)] [string]$Target,
    [string]$SiteSetup = '',
    [string]$AdminEmail = 'admin@lab.local',
    [string]$AdminPassword = ''
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $repo 'X_ChemicalStorage\X_ChemicalStorage.csproj'
function Ok($m)   { Write-Host "  [OK]   $m" -ForegroundColor Green }
function Todo($m) { Write-Host "  [!!]   $m" -ForegroundColor Yellow; $script:todo++ }
$todo = 0

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet مش موجود. لازم Visual Studio أو .NET SDK على الجهاز ده.' }

# ---- site-setup.json: من الـ Downloads لو مش متحدد
if (-not $SiteSetup) {
    $dl = Join-Path $env:USERPROFILE 'Downloads'
    $SiteSetup = Get-ChildItem $dl -Filter 'site-setup*.json' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $SiteSetup -or -not (Test-Path $SiteSetup)) {
    throw 'مش لاقي site-setup.json. من البرنامج: Settings ← System ← Download site-setup.json، وبعدين شغّلي السكريبت تاني.'
}
$setup = Get-Content $SiteSetup -Raw -Encoding UTF8 | ConvertFrom-Json
$fast = @($setup.freezers | Where-Object { $_.tempAlarmDelayMinutes -le 1 -or $_.doorOpenMinutes -le 1 })
if ($fast.Count) { throw "site-setup.json فيه مهلات التجربة (دقيقة) على: $($fast.id -join ', '). رجّعي Normal delays من Settings ← Alarms ونزّلي الملف تاني." }

if (-not $AdminPassword) {
    $sec = Read-Host 'باسورد الأدمن للبرنامج في المعمل (6 حروف على الأقل)' -AsSecureString
    $AdminPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
}
if ($AdminPassword.Length -lt 6) { throw 'الباسورد لازم 6 حروف على الأقل.' }

# ---- publish
$app = Join-Path $Target 'app'
Write-Host "`nPublish (Release, win-x64, self-contained) ← $app" -ForegroundColor Cyan
if (Test-Path $app) { Remove-Item $app -Recurse -Force }
dotnet publish $proj -c Release -r win-x64 --self-contained true -o $app
if ($LASTEXITCODE -ne 0) { throw 'الـ publish فشل. ابعتي آخر سطور الخطأ.' }
# ملف إعدادات جهازك متنقلش للموقع
Remove-Item (Join-Path $app 'appsettings.Development.json') -ErrorAction SilentlyContinue
Ok 'البرنامج'

# ---- إعدادات الموقع: باسورد الأدمن بس، والباقي من appsettings.json (Telegram، MQTT من غير باسورد)
$local = [ordered]@{ AdminSeed = [ordered]@{ UserName = 'admin'; Email = $AdminEmail; Password = $AdminPassword } }
[IO.File]::WriteAllText((Join-Path $app 'appsettings.Local.json'), ($local | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
Ok "appsettings.Local.json (الأدمن: admin / $AdminEmail)"

Copy-Item $SiteSetup (Join-Path $app 'site-setup.json') -Force
$linked = @($setup.freezers | Where-Object collectorCode).Count
Ok "site-setup.json: $(@($setup.freezers).Count) فريزر، $linked مربوط، $(@($setup.contacts).Count) جهة اتصال"

# ---- السكريبتات
$dep = Join-Path $Target 'deploy'
New-Item -ItemType Directory -Path $dep -Force | Out-Null
Copy-Item (Join-Path $PSScriptRoot '*.ps1') $dep -Force
Copy-Item (Join-Path $PSScriptRoot 'gateways.csv') $dep -Force
Copy-Item (Join-Path $repo 'DEPLOYMENT.md') $Target -Force
Move-Item (Join-Path $dep 'install-all.ps1') $Target -Force
Copy-Item (Join-Path $PSScriptRoot 'INSTALL.cmd') $Target -Force
Remove-Item (Join-Path $dep 'make-site-package.ps1'), (Join-Path $dep 'INSTALL.cmd') -ErrorAction SilentlyContinue
Ok 'السكريبتات + INSTALL.cmd'

# ---- برامج التنصيب
$inst = Join-Path $Target 'installers'
New-Item -ItemType Directory -Path $inst -Force | Out-Null
if (Get-ChildItem $inst -Filter 'mosquitto*.exe' -ErrorAction SilentlyContinue) { Ok 'Mosquitto installer' }
else { Todo "حطي تنصيب Mosquitto في $inst  ←  https://mosquitto.org/download  (mosquitto-...-install-windows-x64.exe)" }
if (Get-ChildItem $inst -Filter 'SQLEXPR*.exe' -ErrorAction SilentlyContinue) { Ok 'SQL Server Express installer' }
else { Todo "حطي SQLEXPR_x64_ENU.exe في $inst  ←  نزّلي SQL Server 2022 Express ← Download Media ← Express Core" }

Write-Host ''
if ($todo -eq 0) { Write-Host "الباكدج جاهزة في $Target. في المعمل: كليك يمين على INSTALL.cmd ← Run as administrator." -ForegroundColor Green }
else { Write-Host "ناقص $todo حاجة فوق. حطيها وشغّلي السكريبت تاني (أو كمّلي بإيدك)." -ForegroundColor Yellow }
