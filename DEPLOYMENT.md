# دليل تنصيب X Chemical Storage — Bio Bank على جهاز المعمل

النظام بيشتغل على جهاز واحد في كل معمل:

```
الفريزرات ──► البوابة (Gateway) ──MQTT──► Mosquitto ──► X_ChemicalStorage (Windows Service) ──► SQL Server Express
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

### 2.4 ربط الفريزرات الحقيقية
1. البوابة لازم تبعت على:
   - **Broker:** IP جهاز المعمل، بورت `1883`، باليوزر والباسورد بتوع الخطوة 2.2
   - **Topic:** `freezer/<كود-الفريزر>/telemetry`
   - **Payload (JSON):** `{"temperature": -79.8, "door": "closed", "power": "ok"}`

   أسماء الحقول مرنة. لو البوابة بتبعت أسماء تانية أو الحرارة مضروبة في 10، بتتظبط في `appsettings.Local.json` تحت `Mqtt.Fields` من غير تعديل كود.
2. علشان تشوفي اللي البوابة بتبعته فعلاً:
   ```powershell
   & 'C:\Program Files\mosquitto\mosquitto_sub.exe' -h 127.0.0.1 -t 'freezer/#' -v -u biobank -P 'الباسورد'
   ```
3. من الأبلكيشن: اربطي كل collector بالفريزر بتاعه، وظبطي حدود الحرارة ومهلة انقطاع الاتصال.

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
