# دليل تنصيب X Chemical Storage — Bio Bank على جهاز المعمل

النظام بيشتغل على جهاز واحد في كل معمل:

```
الفريزرات ──RS485──► بوابة هاير IG502 ──LAN/MQTT──► Mosquitto ──► X_ChemicalStorage (Windows Service) ──► SQL Server Express
                                                               │
                                                               ├── إنذارات: Telegram / SMS / Email
                                                               └── Backup يومي 02:00 ──► D:\BioBank_Backups (+ نسخة تانية)
```

الملفات اللي هتحتاجيها كلها في فولدر `deploy/`، وبتتشغل بالترتيب.

---

## المرحلة 0 — قبل أي حاجة (مرة واحدة، النهارده)

الريبو كان public وكان فيه أسرار. شيلناها من الكود، بس هي لسه موجودة في الـ git history، فلازم تتغير:

1. **Telegram bot token:** افتحي Telegram ← `@BotFather` ← `/mybots` ← اختاري البوت ← `API Token` ← `Revoke current token`. التوكن الجديد بيتحط في `appsettings.Local.json` بس.
2. **باسورد قاعدة البيانات على databaseasp.net:** غيريه من لوحة تحكم الاستضافة.
3. **المستخدمين التجريبيين** (`admin@test.com` / `user@test.com`): مبقوش بيتعملوا غير في وضع التطوير. لو موجودين على أي سيرفر شغال، غيري باسوردهم أو امسحيهم.

> قاعدة ثابتة: أي باسورد أو token مكانه `appsettings.Local.json` (متجاهَل من git)، عمره ما يتحط في `appsettings.json`.

---

## المرحلة 1 — على جهازك (قبل ما تروحي الموقع)

### 1.1 بناء المشروع
1. اعملي `git pull`، وافتحي `X_ChemicalStorage.slnx` في Visual Studio.
2. `Build > Rebuild Solution`. أول مرة هينزل باكدج جديدة: `Microsoft.Extensions.Hosting.WindowsServices`.
3. شغّلي (F5). في التطوير البرنامج بيستخدم LocalDB وبيعمل الداتابيز لوحده (`Startup:AutoMigrate`)، وبيعمل مستخدمين التجربة.

### 1.2 تجربة الدورة كاملة بالمحاكي
1. نصّبي Mosquitto على جهازك: https://mosquitto.org/download (Windows 64-bit).
2. افتحي PowerShell في فولدر `deploy` وشغّلي:
   ```powershell
   .\simulate-freezers.ps1
   ```
   ده بيبعت قراءات 3 فريزرات وهمية: `SIM-80A` و `SIM-20A` و `SIM-04A`.
3. من الأبلكيشن ← Bio Bank: هتلاقي الأكواد دي ظاهرة كـ collectors مش مربوطة. اربطي كل كود بفريزر، وظبطي حدود الحرارة.
4. جرّبي كل سيناريو، واتأكدي إن الإنذار بيظهر ورسالة Telegram بتوصل:

   | الأمر | المتوقع |
   |---|---|
   | `.\simulate-freezers.ps1 -Scenario HighTemp -Freezer SIM-80A` | إنذار حرارة عالية |
   | `.\simulate-freezers.ps1 -Scenario DoorOpen` | إنذار باب مفتوح |
   | `.\simulate-freezers.ps1 -Scenario PowerFail` | إنذار انقطاع كهربا |
   | `.\simulate-freezers.ps1 -Scenario Offline` | إنذار انقطاع اتصال بعد المهلة |
   | رجّعي `Normal` | الإنذار يتقفل ورسالة "cleared" توصل |

> لو أي سيناريو منهم مشتغلش على جهازك، **متروحيش الموقع**. صلّحيه الأول.

### 1.3 حضّري معاكي
- فلاشة فيها: الريبو، وتنصيب SQL Server Express، وتنصيب Mosquitto.
- محوّل USB-to-RS485 احتياطي (لو البوابة هتتوصل بالجهاز).
- كابل شبكة.
- ورقة فيها: باسورد الأدمن، Telegram token الجديد، وباسورد MQTT.

---

## المرحلة 2 — في الموقع (جهاز المعمل)

كل السكريبتات بتتشغل من **PowerShell كـ Administrator**. لو ظهر خطأ Execution Policy، شغّلي الأول:
```powershell
Set-ExecutionPolicy -Scope Process Bypass
```

### 2.1 تجهيز الجهاز
1. نصّبي **SQL Server Express** (اختيار Basic، واسم الـ instance يفضل `SQLEXPRESS`).
2. نصّبي **Mosquitto** (علّمي على Service).
3. شغّلي:
   ```powershell
   .\1-prepare-machine.ps1                          # لو مفيش D: ← -BackupDir C:\BioBank_Backups
   ```
   لازم يخلص من غير `[!!]`.

### 2.2 الـ MQTT broker
```powershell
.\2-configure-mosquitto.ps1 -User biobank -Password 'باسورد-قوي'
```
اكتبي اليوزر والباسورد، لأنهم نفسهم هيتحطوا في البوابة وفي `appsettings.Local.json`.

### 2.3 تنصيب البرنامج
1. شغّلي:
   ```powershell
   .\3-install-service.ps1
   ```
   (لو مفيش .NET SDK على الجهاز، اعملي Publish من Visual Studio على جهازك: Folder، Release، win-x64، Self-contained. انقلي الفولدر وشغّلي `.\3-install-service.ps1 -PublishDir D:\publish`.)
2. أول مرة هيفتحلك `appsettings.Local.json` في Notepad. املي:
   - `AdminSeed.Password` ← باسورد أدمن قوي (8 حروف على الأقل، فيه كابيتال وسمول ورقم ورمز)
   - `Notifications.Telegram.BotToken` ← التوكن الجديد
   - `Mqtt.Username` و `Mqtt.Password` ← زي الخطوة 2.2
   - `Backup.Directory` و `Backup.SecondaryDirectory` (فلاشة أو NAS أو درايف تاني)
3. احفظي، وشغّلي `.\3-install-service.ps1` تاني. المرة دي هيعمل الخدمة ويشغّلها.
4. افتحي `http://localhost:5000` وسجّلي دخول بالأدمن.

### 2.4 ربط الفريزرات الحقيقية (بوابات هاير InHand IG502)

كل فريزر عليه بوابة IG502 متوصلة بيه RS485 (Modbus). البوابة عندها بورت LAN، وهي اللي بتبعت MQTT لجهاز المعمل. مفيش أي برنامج إضافي.

**خطة الشبكة** (الأرقام دي موجودة كمان في `deploy/gateways.csv`):

| الفريزر | Logger | كود الـ Collector | IP البوابة | Topic | في الأبلكيشن |
|---|---|---|---|---|---|
| Freezer 1 | 44088274 | GF5022544088274 | 192.168.2.21 | `freezer/GF5022544088274/telemetry` | FZ80-01 |
| Freezer 2 | 44088273 | GF5022544088273 | 192.168.2.22 | `freezer/GF5022544088273/telemetry` | FZ80-02 |
| Freezer 3 | 44088278 | GF5022544088278 | 192.168.2.23 | `freezer/GF5022544088278/telemetry` | FZ80-03 |
| Freezer 4 | 44088271 | GF5022544088271 | 192.168.2.24 | `freezer/GF5022544088271/telemetry` | FZ80-04 ⚠ |
| Freezer 5 | 44088270 | GF5022544088270 | 192.168.2.25 | `freezer/GF5022544088270/telemetry` | FZ150-01 |
| Freezer 6 | 44088272 | GF5022544088272 | 192.168.2.26 | `freezer/GF5022544088272/telemetry` | FZ150-02 |

- **كود الـ Collector = `GF50225` + رقم الـ Logger.** ده الكود اللي البوابات الحقيقية بعتته قبل كده (FZ80-01 و FZ80-02 و FZ150-02 مربوطين بيه فعلاً). اتأكدي منه على شاشة كل بوابة (Serial Number) قبل ما تحطيه في الـ Topic.
- ⚠ **Freezer 4** متسجل في الأبلكيشن ‎-80 (FZ80-04)، بس سحابة هاير بتقول إنه بيقرا ‎-21.7. لو ده حقيقي، هيعمل إنذار حرارة أول ما يتوصل، ولازم يتراجع في الموقع.
- Client ID اختياري التسمية، بس لازم يكون مختلف لكل بوابة.

- **جهاز المعمل:** IP ثابت `192.168.2.10`، Subnet `255.255.255.0`. لو عليه نت من كارت تاني (4G/Wi-Fi)، سيبي Default Gateway فاضي على كارت الـ LAN ده.
- **سويتش** 8 بورت + كابل لكل بوابة.

> ⚠ كل البوابات طالعة من المصنع بنفس الـ IP `192.168.2.1`، وكلها بتوزّع IPs (DHCP). **متوصليش أي بوابة بالسويتش قبل ما تظبطيها لوحدها**، وإلا الشبكة هتضرب.

**ظبط كل بوابة — واحدة واحدة:**
1. لابتوب بكابل مباشر في بورت LAN بتاع البوابة. IP اللابتوب يدوي: `192.168.2.50` / `255.255.255.0`.
2. افتحي `http://192.168.2.1`، يوزر `adm`، باسورد `123456`.
3. **صوّري كل الإعدادات قبل ما تغيّري أي حاجة**، خصوصاً صفحات Modbus/Data Collection والـ Cloud. دي إعدادات هاير الأصلية ولو احتجتي ترجّعيها.
4. **غيّري باسورد الدخول** للبوابة (`adm/123456` معروف للكل).
5. **Network → LAN:** الـ IP من الجدول (مثلاً `192.168.2.21`)، و**اقفلي DHCP Server**. Apply. (اللابتوب هيفقد الاتصال؛ ادخلي تاني على الـ IP الجديد.)
6. **Edge Computing / Data Collection → Cloud / MQTT:**
   - Server: `192.168.2.10`، Port: `1883`
   - Username / Password: بتوع الخطوة 2.2
   - Client ID و Topic: من الجدول بالظبط
   - QoS `1`، Retain `False`، Keep Alive `60`، Reporting Interval `10–30` ثانية
7. **سحابة هاير (Device Platform / InCloud) والـ 4G:** القرار ده بتاع العميل. لو اتقفلوا، خدمة هاير عن بُعد هتقف، وجهاز المعمل محتاج نت من مصدر تاني عشان Telegram.
8. **متغيّريش إعدادات الـ Serial / Modbus** (Baud rate وغيره). دي هاير مظبطاها ومتوصلة باللوحة.
9. Save / Apply، وبعدها وصّلي البوابة بالسويتش، وروحي للي بعدها.

**التأكد من الكل مرة واحدة** (من جهاز المعمل):
```powershell
.\5-check-gateways.ps1 -User biobank -Password 'الباسورد'
```
بيقولك لكل بوابة: على الشبكة ولا لأ، وبتبعت ولا لأ، والحرارة كام، وبيطبعلك شكل رسالة حقيقية.

**لو أسماء الحقول في الرسالة مختلفة** (مثلاً `Temp_PV` بدل `temperature`)، أو الحرارة مضروبة في 10، مفيش تعديل كود. ضيفي في `appsettings.Local.json`:
```json
"Mqtt": {
  "Fields": {
    "Temperature": [ "Temp_PV" ],
    "TemperatureScale": 0.1
  }
}
```
وأعيدي تشغيل الخدمة (`Restart-Service XChemicalStorage`).

**في الأبلكيشن:** هتلاقي الـ Loggers ظاهرة في `biobank/station` كـ collectors مش مربوطة. اربطي كل Logger بالفريزر بتاعه، وحطي الحدود والمهلات الحقيقية.

### 2.5 اختبارات القبول (قبل ما تمشي)
| # | الاختبار | المتوقع |
|---|---|---|
| 1 | `.\4-check-health.ps1` | كله `[OK]` |
| 2 | افصلي كابل البوابة | إنذار انقطاع خلال المهلة، ورسالة Telegram |
| 3 | افتحي باب فريزر | إنذار باب مفتوح |
| 4 | Restart للجهاز | البرنامج يرجع لوحده من غير ما حد يفتحه |
| 5 | افصلي كهربا الجهاز (لو فيه UPS) | الجهاز يكمل على الـ UPS |
| 6 | من الأبلكيشن ← Backup ← تشغيل الآن | ملف `.bak` يظهر في الفولدرين |
| 7 | جهاز تاني على نفس الشبكة يفتح `http://IP-الجهاز:5000` | الصفحة تفتح |

---

## مشاكل شائعة

| المشكلة | الحل |
|---|---|
| الخدمة مش بتقوم | Event Viewer ← Windows Logs ← Application، وابحثي عن `XChemicalStorage` |
| `Login failed for user 'NT AUTHORITY\SYSTEM'` | في SSMS: Security ← Logins ← `NT AUTHORITY\SYSTEM` ← Server Roles ← `dbcreator` |
| `/healthz` بيقول `down` | استني دقيقتين بعد التشغيل. لو فضلت كده، Mosquitto واقف أو اليوزر والباسورد غلط |
| الـ Backup بيفشل (Access denied) | شغّلي `1-prepare-machine.ps1` تاني بعد تنصيب SQL Express، عشان يدي SQL صلاحية الفولدر |
| مفيش رسايل Telegram | الجهاز عليه نت؟ التوكن الجديد اتحط؟ الـ chat id بتاع كل مستلم متسجل في الأبلكيشن؟ |
| الفريزر ظاهر بس الحرارة غلط | ظبطي `Mqtt.Fields.TemperatureScale` (مثلاً `0.1`) |

## تحديث البرنامج بعدين
`git pull` ← `.\3-install-service.ps1`. السكريبت بيوقف الخدمة، وينسخ النسخة الجديدة، ويحافظ على `appsettings.Local.json` والملفات المرفوعة، والداتابيز بتتحدث لوحدها.
