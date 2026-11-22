# D-099 فعال‌سازی مجدد سخت‌افزاری — معیارهای پذیرش قابل آزمون و ماتریس تست — `Proposed`

> **وضعیت سند:** خروجی S-E مأموریت «ادامهٔ کار Vendor». D-099 برای «رفتار محصول» `Accepted` است (تأیید مالک، Issue #696) اما پیاده‌سازی `NotImplemented` است. این سند تصمیم جدیدی نمی‌گیرد: پیش‌نیازهای واقعی را استخراج، رفتار پذیرفته‌شده را به معیارهای آزمون‌پذیر ترجمه و مرز «قابل تست با mock/SoftHSM» از «نیازمند سخت‌افزار واقعی» را مشخص می‌کند. هیچ ادعای Production Hardware Readiness ثبت نمی‌شود.

## ۱) آنچه D-099 پذیرفته است (خلاصهٔ الزام‌آور؛ متن کامل: `docs/DECISIONS.md` §D-099)

1. کلید خصوصی Installation هرگز به Vendor ارسال نمی‌شود؛ اثبات هویت فقط با possession کلید (challenge/امضا) معتبر است — اثرانگشت سخت‌افزار به‌تنهایی اثبات رمزنگاری نیست (بند ۱ و ۶).
2. مشخصات سخت‌افزار برای مقایسه در Vendor جمع‌آوری و تاریخچهٔ هر Installation نگه داشته می‌شود؛ محدودهٔ داده و نگهداری = OD-20 (`NeedsApproval`) (بند ۲ و ۸).
3. نصب مجدد Windows نیازمند فعال‌سازی مجدد است؛ تغییر RAM/کارت شبکه به‌تنهایی نباید قفل کند؛ تشخیص تغییر باید **نسخه‌دار و پیکربندی‌شده** باشد، نه hardcoded (بند ۳ و ۴).
4. TPM ترجیحی است ولی اجباری نیست؛ مسیر `SoftwareProtected` با رضایت صریح طبق D-058 باقی است (بند ۵).
5. فعال‌سازی مجدد Online و Offline هر دو نیازمند تأیید صریح مالک Vendor است؛ بدون تأیید هیچ پاسخ امضاشده‌ای صادر نمی‌شود (بند ۷).
6. پاسخ جدید باید به Installation، درخواست (requestId/nonce)، کلید جدید (SPKI/key version) و License revision bind شود؛ reuse/انتقال رد می‌شود (بند ۱۰).
7. تأیید/رد باید هویت تأییدکننده، زمان و علت را Audit کند (maker-checker D-046؛ ممیزی D-043/D-070)؛ رد به معنی حذف داده یا revocation نیست (بند ۹ و ۱۱).

## ۲) وضعیت واقعی کد و تفاوت مسیرهای Software/Hardware (main@`58a1ccd4`)

- **سمت Customer:** هویت نصب با CNG ECDSA P-256 non-exportable MachineKey (D-058؛ TPM 2.0/Platform Crypto Provider پیش‌فرض، Microsoft Software KSP فقط با رضایت صریح و ثبت `AssuranceLevel` پایین‌تر). موجود در کد: `WindowsCngLicenseInstallationKeyCommand`، `WindowsCngLocalInstallationKeyMetadataProvider`، `IInstallationIdentityProofVerifier` با impl `WindowsCngLicenseInstallationIdentityProofVerifier` و قرارداد فایل درخواست نسخه‌دار `.request.json` (الگوی D-060 §11). گردش‌کار reactivation (جمع‌آوری مشخصات، صف تأیید، پاسخ bind‌شده) وجود ندارد.
- **سمت Vendor:** signer مرکزی CNG/Certificate Store با سطح صریح `SoftwareProtected` طبق D-107 در کد است و با `Signing:CertificateStore:Enabled` opt-in می‌شود؛ Key Ceremony واقعی، ACL هویت سرویس، rotation/overlap و recovery روی میزبان Production انجام نشده‌اند (runbookهای موجود: `docs/runbooks/ISSUE-236-CNG-KEY-CEREMONY-ROTATION.md` و `ISSUE-236-HSM-TPM-KEY-OPERATIONS.md`). امضای لایسنس در GitHub Actions ممنوع است (D-041/G5) و مسیر HSM/PKCS#11 برای signer ابطال آفلاین طبق D-055 با `Signing:Pkcs11:Enabled` جدا است و هنوز عملیاتی نشده است.
- **تفاوت دو مسیر امضا:** مسیر `HardwareTpm` (کلید در TPM، assurance بالاتر، بدون نیاز به رضایت ceremony جدا) در برابر `SoftwareProtected` (Microsoft Software KSP/DPAPI، assurance پایین‌تر، ریسک مدیر محلی/clone، فقط با رضایت صریح و ثبت سطح). هر دو مسیر باید در accept criteria و ماتریس تست جدا دیده شوند؛ هیچ fallback خودکار مجاز نیست (D-058).
- **وضعیت تست فعلی:** تست‌های SQL Server و Windows CNG در CI سبز اجرا می‌شوند؛ SoftHSM فقط در محیط CI (محلی ۷۸ Skip ثبت‌شده در PR #753). هیچ تستی برای reactivation وجود ندارد (قابلیت پیاده نشده).

## ۳) پیش‌نیازهای واقعی Hardware Reactivation (پیش از هر پیاده‌سازی)

| # | پیش‌نیاز | نوع | مرجع |
|---|---|---|---|
| P1 | تعریف قرارداد نسخه‌دار درخواست/پاسخ reactivation (توسعهٔ `.request.json`؛ requestId/nonce، مشخصات نسخه‌دار سخت‌افزار، SPKI کلید جدید، License revision هدف) | قرارداد بین‌بخشی؛ بدون پروتکل موازی | D-099 «مسیر پیاده‌سازی»، D-060 §11 |
| P2 | تأیید محدودهٔ دادهٔ سخت‌افزاری و مدت نگهداری در Vendor | تصمیم مالک | OD-20، D-070 |
| P3 | پیکربندی نسخه‌دار تشخیص تغییر سخت‌افزار (وزن/فهرست مؤلفه/آستانه) | پیکربندی + تست | D-099 بند ۴ |
| P4 | صف تأیید مالک در Vendor (CRM سبک) با maker-checker و audit کامل | کد Vendor + فرایند عملیاتی | D-099 بند ۷/۸/۱۱، D-046، D-102 بند ۶ |
| P5 | صدور پاسخ امضاشدهٔ bind‌شده و reject reuse | قرارداد + کد + تست | D-099 بند ۱۰، T49 |
| P6 | شواهد بازیابی در گم‌شدن کلید قبلی | تصمیم مالک؛ خارج از این سند | OD-21، OD-06 |
| P7 | مسیر SoftwareProtected و HardwareTpm هر دو پشتیبانی شوند؛ هیچ fallback خودکار | کد + تست | D-058، D-107 |
| P8 | Key Ceremony واقعی و کلید عملیاتی روی میزبان Production Vendor | زیرساخت/مالک | #236، runbookهای موجود |

## ۴) معیارهای پذیرش قابل آزمون (Acceptance Criteria)

هر بند با «شاهد قابل اجرا» تعریف می‌شود؛ بدون برآورده‌شدن همهٔ ACها قابلیت نباید Implemented اعلام شود:

| AC | معیار (رفتار الزامی) | نگاشت |
|---|---|---|
| AC-1 | درخواست reactivation بدون possession کلید نصب (امضای challenge نامعتبر/غایب) در Customer **و** Vendor رد شود؛ هیچ مسیر امضای جایگزین وجود نداشته باشد | D-099 بند ۱/۶ |
| AC-2 | تغییر RAM یا کارت شبکه به‌تنهایی درخواست reactivation تولید نکند؛ تغییر نسخهٔ OS-installation marker درخواست تولید کند | بند ۳/۴ |
| AC-3 | تشخیص تغییر سخت‌افزار کاملاً از پیکربندی نسخه‌دار بخواند (بدون threshold hardcoded)؛ تغییر پیکربندی بدون تغییر کد رفتار را عوض کند | بند ۴ |
| AC-4 | تا تأیید صریح مالک، هیچ پاسخ امضاشده‌ای — Online یا Offline — صادر نشود؛ صف تأیید بدون تأیید فقط Pending بماند | بند ۷/۸ |
| AC-5 | پاسخ صادرشده فقط با چهار binder معتبر پذیرفته شود: Installation، requestId/nonce، SPKI کلید جدید، License revision؛ reuse برای درخواست دیگر یا revision دیگر رد شود | بند ۱۰ |
| AC-6 | تأیید و رد هر دو با هویت تأییدکننده، زمان UTC و علت در Audit ثبت شوند؛ maker-checker (D-046) رعایت شود | بند ۱۱ |
| AC-7 | رد درخواست هیچ داده‌ای حذف نکند؛ وضعیت لایسنس/Installation دست‌نخورده بماند | بند ۹ |
| AC-8a | مسیر `HardwareTpm` — **مستقل و fail-closed:** نبود TPM یا نبود/ناهمخوانی کلید سخت‌افزاری مورد انتظار، اعتبارسنجی هویت را با کد روشن رد کند (رفتار موجود: `Unavailable` در `WindowsCngLicenseInstallationIdentityProofVerifier.cs:71-73`)؛ هیچ تغییر مسیر خودکار به Software KSP یا هر روش ضعیف‌تر مجاز نیست و شکست اعتبارسنجی سخت‌افزار هرگز نباید به فعال‌سازی بدون کنترل منجر شود | بند ۵، D-058، OD-08 |
| AC-8b | مسیر `SoftwareProtected` — **مستقل از مسیر ۸a:** استفاده از Software KSP فقط وقتی مجاز است که این سطح assurance صریحاً انتخاب و تأیید شده باشد (رضایت ثبت‌شده: `SoftwareProviderRiskAcceptedAtUtc` + `SoftwareProviderRiskAcceptedBy`؛ الزام در `WindowsCngLicenseInstallationKeyProvisioner.cs:66/105` و verifier `:75-78`)؛ **نبود TPM به‌تنهایی نباید باعث شکست این مسیر شود**؛ سطح assurance باید در وضعیت (`ApiEndpoints.cs:766` `AssuranceLevel`) و گزارش‌های مربوط قابل تشخیص باشد؛ **هیچ downgrade خودکاری از `HardwareTpm` به `SoftwareProtected` مجاز نیست** — تغییر سطح فقط با فرایند صریح re-enrollment/مجوز، نه fallback بی‌صدا | بند ۵، D-058، OD-08 |
| AC-9 | هر رخداد (درخواست/تأیید/رد/صدور پاسخ) ممیزی نسخه‌دار داشته باشد و در Vendor history هر Installation قابل مشاهده باشد | بند ۲/۸ |
| AC-10 | عدم ادغام با revocation: ردِ reactivation، لایسنس را revoke نکند و پروتکل ابطال آفلاین (D-057/D-055) را bypass نکند | مرز معماری |

**استقلال دو مسیر AC-8 (اصلاح ابهام AC-8 نسخهٔ اول):** `HardwareTpm` (AC-8a) و `SoftwareProtected` (AC-8b) دو مسیر مستقل‌اند و هیچ رفتار مشترکِ «یکی به‌جای دیگری» ندارند: در مسیر ۸a نبود TPM/کلید سخت‌افزاری fail-closed است و نباید خودکار به Software KSP بیفتد؛ در مسیر ۸b نبود TPM به‌تنهایی شکست نیست، اما استفاده از Software KSP مشروط به انتخاب و تأیید صریح همان سطح assurance است و سطح انتخابی در وضعیت/گزارش‌ها قابل تشخیص می‌ماند. هیچ مسیری از ۸a به ۸b (یا برعکس) به‌صورت خودکار تغییر نمی‌کند؛ تبدیل سطح فقط re-enrollment صریح است. متن D-058 «هیچ fallback خودکار یا تغییر provider بی‌صدا مجاز نیست» مرجع هر دو زیرمعیار است.

## ۵) ماتریس تست — چه چیزی با mock/SoftHSM و چه چیزی با سخت‌افزار واقعی

| سطح | پوشش تست | محیط لازم | وضعیت امروز |
|---|---|---|---|
| T-A (کاملاً قابل انجام اکنون) | منطق خالص: اعتبارسنجی قرارداد نسخه‌دار درخواست/پاسخ، binderهای AC-5، reject replay، رد possession نامعتبر، رد علامت‌گذاری‌های ناقص، state transitions صف تأیید، audit fields — با درون‌ریزی signer/verifier و clock تزریقی | Unit testها روی `Vendor.sln` + `Template.Tests`؛ هیچ سخت‌افزاری لازم نیست | قابل شروع پس از تأیید قرارداد (P1) |
| T-B (SoftHSM / شبیه‌ساز) | مسیر PKCS#11/HSM vendor-side برای هر بخشی که به کلید امضای vendor وصل می‌شود؛ رفتار fail-closed در نبود provider | SoftHSM در CI (الگوی موجود تست‌های Skip-local) | زیرساخت تست موجود، قابلیت هنوز پیاده نشده |
| T-C (Windows CNG واقعی در CI) | MachineKey non-exportable، `ExportPolicy=None`، **مسیر `SoftwareProtected` و AC-8b** (نیاز به رضایت ثبت‌شده؛ رد بدون رضایت؛ نبود TPM این مسیر را نمی‌شکند)، proof-of-possession با کلید واقعی | Windows runner CI (الگوی موجود: تست‌های CNG #753 در CI سبز) | زیرساخت موجود |
| T-D (سخت‌افزار واقعی — فقط دستی/Stage) | **مسیر `HardwareTpm` و AC-8a**: TPM 2.0 واقعی (Platform Crypto Provider)، fail-closed در نبود TPM و رد هر fallback خودکار، re-image واقعی Windows، restore از backup، دو میزبان Vendor/Customer | میزبان Windows فیزیکی/VM با TPM + میزبان Vendor؛ ثبت شواهد در runbook | **انجام‌نشده و خارج از محیط فعلی؛ هیچ ادعای Production readiness بدون آن مجاز نیست** |

قواعد ماتریس: هیچ ACای نباید فقط با T-A بسته اعلام شود اگر به کلید واقعی وابسته است (مثلاً AC-1/AC-8a/AC-8b به T-C یا T-D نیاز دارند)؛ رد شدن در T-B/C هرگز با mock «سبز» پوشانده نشود؛ تست‌های وابسته به SQL Server/SoftHSM/HSM فقط در CI/Stage معتبرند (تفاوت با InMemory صریح ثبت شود).

## ۶) الزامات Production fail-closed و مرزهای حفظ‌شده

- سطح‌بندی اطمینان امضای vendor طبق D-107 (`SoftwareProtected` پذیرفته‌شده؛ `HardwareProtected` نیازمند Key Ceremony واقعی) — هیچ ارتقای سطح بدون شواهد واقعی.
- signer ابطال آفلاین مسیر مستقل D-055/D-057 دارد و با این قابلیت ادغام نمی‌شود؛ OD-27 (نصب فاقد TPM) همچنان `NeedsApproval` است و این سند آن را حل نمی‌کند.
- امضای لایسنس هرگز در CI/GitHub Actions انجام نمی‌شود (D-041/G5)؛ کلید صدور در اسرار Workflow نیست.
- **جملهٔ صریح:** تا اجرای Key Ceremony و تست‌های T-D، «آماده بودن Production Hardware Reactivation» وجود ندارد و هیچ سند/کدی نباید چنین ادعایی ثبت کند.
