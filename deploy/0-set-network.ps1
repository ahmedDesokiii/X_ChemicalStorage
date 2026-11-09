<#
  الخطوة 0 — ضبط كارت الشبكة بتاع جهاز المعمل على شبكة البوابات (PowerShell كـ Administrator)

  قبل ما تشغليه: وصّلي كابل من الجهاز للسويتش (مش لازم البوابات تكون متوصلة لسه).

  .\0-set-network.ps1                 # يعرض كروت الشبكة ويختار كارت الكابل لوحده
  .\0-set-network.ps1 -Adapter 'Ethernet 2'   # لو الجهاز فيه أكتر من كارت كابل
  .\0-set-network.ps1 -ListOnly       # يعرض الكروت بس من غير ما يغير حاجة

  بيعمل:
   - IP ثابت 192.168.2.10 / 255.255.255.0 من غير Default Gateway
     (عشان النت — لو موجود من Wi-Fi أو 4G — يفضل شغال من الكارت التاني)
   - يخلي الشبكة دي Private
   - يجرّب يوصل للبوابات (ping) لو متوصلة
#>
param(
    [string]$Adapter = '',
    [string]$IP = '192.168.2.10',
    [int]$Prefix = 24,
    [switch]$ListOnly
)
$ErrorActionPreference = 'Stop'

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) -and -not $ListOnly) {
    throw 'شغّلي PowerShell كـ Administrator.'
}

$all = Get-NetAdapter -Physical | Sort-Object Name
Write-Host "`n== كروت الشبكة في الجهاز ==" -ForegroundColor Cyan
foreach ($a in $all) {
    $ip = (Get-NetIPAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).IPAddress -join ', '
    $kind = if ($a.PhysicalMediaType -match '802\.3' -or $a.MediaType -eq '802.3') { 'كابل' } elseif ($a.PhysicalMediaType -match 'Wireless|802\.11') { 'Wi-Fi' } else { $a.PhysicalMediaType }
    $color = if ($a.Status -eq 'Up') { 'Green' } else { 'DarkGray' }
    Write-Host ("  {0,-22} {1,-8} {2,-14} {3}" -f $a.Name, $kind, $a.Status, $ip) -ForegroundColor $color
}
if ($ListOnly) { return }

if ($Adapter) {
    $nic = $all | Where-Object Name -eq $Adapter
    if (-not $nic) { throw "مفيش كارت اسمه '$Adapter'. الأسماء فوق." }
} else {
    $wired = @($all | Where-Object { $_.Status -eq 'Up' -and ($_.PhysicalMediaType -match '802\.3' -or $_.MediaType -eq '802.3') })
    if ($wired.Count -eq 0) { throw 'مفيش كارت كابل متوصل (Up). اتأكدي إن الكابل في السويتش والسويتش شغال.' }
    if ($wired.Count -gt 1) { throw "فيه أكتر من كارت كابل متوصل: $($wired.Name -join ' / '). حددي واحد: -Adapter 'الاسم'" }
    $nic = $wired[0]
}

Write-Host "`nهظبط الكارت: $($nic.Name)" -ForegroundColor Cyan

# امسحي أي IP قديم أو DHCP على الكارت ده
Set-NetIPInterface -InterfaceIndex $nic.ifIndex -Dhcp Disabled
Get-NetIPAddress -InterfaceIndex $nic.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue
Get-NetRoute -InterfaceIndex $nic.ifIndex -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue

New-NetIPAddress -InterfaceIndex $nic.ifIndex -IPAddress $IP -PrefixLength $Prefix | Out-Null
try { Set-NetConnectionProfile -InterfaceIndex $nic.ifIndex -NetworkCategory Private } catch { }
Write-Host "  [OK]   $($nic.Name) = $IP / $Prefix (من غير Gateway)" -ForegroundColor Green

Start-Sleep 3
Write-Host "`n== البوابات ==" -ForegroundColor Cyan
$csv = Join-Path $PSScriptRoot 'gateways.csv'
$targets = if (Test-Path $csv) { Import-Csv $csv } else { @() }
if (Test-Connection 192.168.2.1 -Count 1 -Quiet) {
    Write-Host '  [!!]   فيه بوابة لسه على 192.168.2.1 (إعداد المصنع). ظبطيها قبل ما توصلي غيرها.' -ForegroundColor Yellow
}
foreach ($g in $targets) {
    $ok = Test-Connection $g.GatewayIP -Count 1 -Quiet
    $mark = if ($ok) { '[OK]' } else { '[--]' }
    $c = if ($ok) { 'Green' } else { 'DarkGray' }
    Write-Host ("  {0}   {1,-10} {2}" -f $mark, $g.Name, $g.GatewayIP) -ForegroundColor $c
}
Write-Host "`n[--] طبيعي للبوابات اللي لسه متظبطتش أو متوصلتش." -ForegroundColor DarkGray
