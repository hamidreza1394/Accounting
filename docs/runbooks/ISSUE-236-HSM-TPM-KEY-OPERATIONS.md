# Runbook عملیات کلید HSM/TPM — Issue #236 / D-055

- **وضعیت:** مستند عملیاتی آمادهٔ بازبینی؛ اجرای Production تا تأمین HSM واقعی و provision شدن backend امن هر نصب مشتری **Blocked** است.
- **دامنه:** کلید مرکزی Vendor برای امضای بسته و لایسنس، و کلید مستقل هر نصب مشتری برای ACK.
- **اصل غیرقابل‌مذاکره:** کلید خصوصی هرگز export، serialize، backup به‌صورت فایل، commit، log یا داخل بستهٔ Provisioning نمی‌شود. هیچ software fallback یا کلید SoftHSM در Production وجود ندارد.
- **مبنای استقرار:** deployment-neutral؛ Vendor می‌تواند روی سرور اختصاصی یا میزبان ابری اجرا شود. این Runbook به Azure/AWS وابسته نیست.

## 1. نقش‌ها و تفکیک مسئولیت

| نقش | مسئولیت | ممنوعیت |
|---|---|---|
| Security Owner | تصویب Key Ceremony، rotation، revoke و بازیابی | دسترسی انفرادی به PIN یا مادهٔ کلید خصوصی |
| HSM Custodian A | کنترل فیزیکی/مدیریتی HSM و اجرای دستورهای مصوب | تأیید عملیات خودش |
| HSM Custodian B | شاهد مستقل و تأیید نفر دوم | استفاده از حساب اپراتور اول |
| Vendor Maker | ثبت درخواست صدور بسته/لایسنس | تأیید درخواست خودش |
| Vendor Checker | تأیید مستقل طبق Maker-Checker موجود | دسترسی به private key |
| Vendor Runtime Identity | فقط جست‌وجوی کلید مجاز و عملیات امضای لازم | مدیریت کلید، export، تغییر policy یا دسترسی مدیریتی HSM |
| Customer Installer Admin | اجرای ceremony محلی ACK و انتقال public key | انتقال private key به Vendor یا نصب دیگر |

دو نفر باید هویت یکدیگر را مستقل تأیید کنند. حساب مشترک، PIN مشترک بین اپراتورها، تأیید self-approval یا ثبت دستی تأیید پس از عملیات مجاز نیست.

## 2. تفکیک کلیدها و قرارداد PKCS#11

برای هر کلید یک KeyId یکتا و نسخه‌دار ثبت شود؛ نمونه‌های نام‌گذاری صرفاً الگو هستند:

- "vendor-package-YYYY-vN" — امضای بستهٔ Provisioning
- "vendor-license-YYYY-vN" — امضای لایسنس
- "customer-INSTALLATION-REFERENCE-ack-vN" — کلید ACK همان نصب، خارج از HSM مرکزی Vendor

کلیدهای بسته و لایسنس باید دو جفت‌کلید مستقل باشند، حتی اگر در یک HSM/partition قرار گیرند. کلید ACK هر نصب نیز مستقل است و هیچ‌گاه بین دو نصب کپی نمی‌شود.

برای private-key objectهای Vendor، Key Ceremony باید این ویژگی‌ها را تأیید کند:

- نوع کلید EC با منحنی P-256 و پارامتر DER مربوط به OID 1.2.840.10045.3.1.7.
- CKA_SIGN=true، CKA_SENSITIVE=true و CKA_EXTRACTABLE=false.
- CKA_ALWAYS_SENSITIVE=true و CKA_NEVER_EXTRACTABLE=true.
- CKA_LABEL و CKA_ID دقیقاً برابر KeyId نسخه‌دار باشند.
- عملیات مجاز Runtime فقط در حد نیاز امضا و خواندن metadata عمومی/صفات مجاز باشد؛ Runtime مجوز مدیریت token، ساخت/حذف کلید، wrap/unwrap یا export کلید نداشته باشد.
- Provider در محیط غیرتستی باید Hardware Slot و manufacturer/model دقیقاً allowlist‌شده را ببیند.

این Runbook دستور ساخت کلید را به CLI خاصی گره نمی‌زند؛ دستورهای واقعی باید از راهنمای همان HSM تأییدشده گرفته شوند. هرگز دستور یا ابزار ناشناخته‌ای را روی token عملیاتی اجرا نکنید.

## 3. پیش‌شرط‌های Key Ceremony

پیش از جلسه:

1. D-055 و Issue مربوط تصویب شده باشند؛ مدل HSM، firmware، مکان فیزیکی، پشتیبانی، HA/DR و هزینه به‌صورت مکتوب تصویب شده باشند.
2. HSM واقعی با firmware تأییدشده، serial/asset ثبت‌شده و slot سخت‌افزاری در دسترس باشد. SoftHSM برای این مرحله ممنوع است.
3. HSM در محیط ایزوله و تحت کنترل دو Custodian قرار گیرد؛ ساعت میزبان و audit logging بررسی شود.
4. هویت اپراتورها، مجوزهای دو نفره، نقش Runtime و روش امن تحویل PIN/credential آماده باشد.
5. Vendor trust store و مشتریان هدف، مسیر ثبت public key و rollback plan مشخص داشته باشند.
6. درخواست Ceremony شامل purpose، KeyId، محیط، token/partition، تاریخ شروع و پایان اعتبار، درخواست‌کننده و تأییدکننده باشد.

## 4. اجرای Key Ceremony برای Vendor

1. دو Custodian هویت، وضعیت فیزیکی/firmware و token/partition را با هم بررسی و ثبت کنند.
2. برای package و license به‌صورت جداگانه جفت‌کلید ECDSA P-256 داخل HSM تولید شود؛ private key باید از ابتدا non-extractable باشد.
3. برای هر private key، KeyId جدید و غیرقابل‌استفادهٔ مجدد تعیین و به‌عنوان CKA_LABEL و CKA_ID ثبت شود.
4. با read-back صفات عمومی، non-extractable بودن، حساسیت، مجوز sign و curve بررسی شود. اگر هر attribute قابل‌خواندن/تأیید نیست، کلید تا رفع ابهام فعال نشود.
5. فقط public key متناظر استخراج شود؛ fingerprint آن با ابزار مستقل محاسبه و توسط هر دو Custodian امضا/تأیید شود.
6. public key و KeyId در trust storeهای Vendor و مصرف‌کنندگان مجاز ثبت شود. هیچ private key یا backup حاوی private key از HSM خارج نشود.
7. Runtime identity فقط دسترسی لازم برای دو KeyId مجاز دریافت کند. دسترسی مدیریتی و عملیات کلید در اختیار Runtime قرار نگیرد.
8. آزمون sign/verify با payload تستی غیرحساس انجام شود؛ سپس هر دو Custodian نتیجه، fingerprint، KeyId، token model، firmware و زمان را در صورت‌جلسه ثبت کنند.
9. خروجی جلسه شامل public keys، fingerprints، تنظیمات غیرمحرمانه و صورت‌جلسه باشد؛ PIN، private key، token backup secret و دادهٔ حساس در گزارش ثبت نشود.

## 5. کلید ACK در نصب مشتری — TPM/OS secure storage

1. کلید ACK روی همان نصب مشتری تولید شود؛ عملیات تولید و امضا باید محلی و بدون اتصال اینترنت/Vendor انجام شود.
2. در Windows از TPM/CNG یا backend محافظت‌شدهٔ سیستم‌عامل و در Linux از TPM2/PKCS#11 محلی یا secure keystore مورد تأیید استفاده شود؛ backend باید non-exportable بودن را اثبات کند.
3. اگر backend امن در دسترس نیست یا صفات non-exportable قابل اثبات نیست، ACK امضاشده فعال نشود و نصب fail-closed بماند.
4. KeyId یکتا و نسخه‌دار به installation reference متصل شود؛ کلید یک نصب برای نصب دیگر قابل استفاده نیست.
5. فقط public key و fingerprint از مسیر درخواست/پاسخ آفلاین ثبت‌شده به Trust Store مشتری و Vendor منتقل شود. private key هرگز ارسال یا backup فایل‌محور نشود.
6. امضای ACK با ECDSA P-256 روی بایت‌های canonical دقیق و بدون تماس شبکه انجام شود.
7. در تعویض مادربرد/TPM، خرابی OS keystore یا نصب مجددی که key handle را از بین می‌برد، کلید قبلی قابل بازیابی فرض نشود: نصب جدید باید کلید تازه بسازد، public key را دوباره enroll کند و KeyId قبلی revoke شود.

### فرمان فعلی Key Provisioning برای Windows

- در نسخهٔ Windows، اپراتور محلی با حسابی که باید مالک/دارندهٔ دسترسی کلید نصب باشد، فرمان زیر را اجرا می‌کند:
  `dotnet Server.dll --provisioning-ack-key create`
- فرمان فقط روی Windows اجرا می‌شود و InstallationId را از تنظیمات معتبر نصب، ProviderName/KeyVersion/MachineKey را از بخش Provisioning:CustomerAcknowledgementSigning می‌خواند.
- خروجی فقط metadata و Base64 کلید عمومی SubjectPublicKeyInfo را چاپ می‌کند؛ private key هرگز export یا نمایش داده نمی‌شود. ایجاد کلید موجود با همان نسخه رد می‌شود؛ برای rotation باید KeyVersion جدید تعیین شود.
- این فرمان Enabled را تغییر نمی‌دهد. تا ثبت و تطبیق public key در Trust Store مشتری و Vendor و تأیید ceremony، گزینهٔ Enabled باید false بماند.
- Provider پیش‌فرض Microsoft Platform Crypto Provider است. اگر TPM/KSP موجود یا قابل‌استفاده نباشد، فرمان fail-closed می‌شود؛ انتخاب Microsoft Software Key Storage Provider فقط با تغییر صریح تنظیمات و پذیرش assurance پایین‌تر مجاز است.
- برای MachineKey=true، کلید را تحت identity سرویس اجرا/ایجاد کنید یا ACL دقیق کلید را طبق سیاست Windows تنظیم کنید؛ دسترسی Administrator عمومی یا حساب‌های غیرمرتبط اضافه نشود. پیکربندی نهایی ACL باید در آزمون استقرار واقعی تأیید شود.

## 6. PIN، credential و Runtime access

- PIN و credential فقط از Secret Store یا Environment امن همان میزبان به process تزریق شوند؛ هرگز در Git، appsettings tracked، command line، package، crash dump یا log قرار نگیرند.
- در Production، فایل Environment یا Secret Store باید ACL محدود به service identity داشته باشد؛ دسترسی خواندن/تغییر credential ثبت و بازبینی شود.
- Runtime identity مجوز sign-only و دسترسی به KeyIdهای تعیین‌شده داشته باشد. حساب Runtime مدیر HSM نباشد.
- تغییر PIN، reset token، unlock، تغییر ACL یا تنظیمات partition فقط در Ceremony جداگانه و با دو نفر انجام شود.
- در log فقط purpose، KeyId عمومی، نتیجه، correlation و زمان ثبت شود؛ payload، signature، PIN، private/public key bytes یا exception message حاوی مسیر/credential ثبت نشود.

## 7. Rotation و هم‌پوشانی Trust Store

1. پیش از rotation، KeyId جدید و بازهٔ اعتبار آن تصویب شود؛ کلید قدیمی حذف یا overwrite نشود.
2. جفت‌کلید جدید داخل HSM تولید و public key آن در Trust Storeهای Vendor و مشتریان هدف اضافه شود.
3. پیش از تغییر signer، verifierهای همهٔ مسیرهای مصرف باید هر دو KeyId جاری و جدید را بپذیرند.
4. signer به KeyId جدید تغییر کند؛ تست sign/verify واقعی و audit ثبت شود.
5. دورهٔ overlap تا پایان عمر artifactهای امضاشدهٔ قدیمی و فرصت توزیع آفلاین ادامه یابد. تاریخ پایان باید بر اساس چرخهٔ انتشار و نصب‌های آفلاین تصویب شود، نه حدس.
6. پس از اثبات پوشش، KeyId قدیمی revoke شود؛ public key قدیمی فقط برای اعتبارسنجی تاریخی/ممیزی نگهداری شود و برای امضای جدید قابل استفاده نباشد.
7. trust snapshot امضاشده و شماره‌نسخه‌دار برای نصب‌های آفلاین منتشر و دریافت آن‌ها در گزارش عملیاتی ثبت شود.

## 8. Revocation و رخداد compromise

در صورت افشای احتمالی، دسترسی غیرمجاز، فقدان کنترل HSM یا خروج غیرمجاز private key:

1. Incident Commander و Security Owner مطلع شوند؛ صدور جدید با KeyId مشکوک فوراً متوقف شود.
2. KeyId در Vendor trust policy به وضعیت revoked منتقل و هر تلاش امضا با آن رد شود.
3. جفت‌کلید جایگزین در Ceremony جدید ساخته شود؛ trust overlap فقط در حدی که امنیت اجازه می‌دهد حفظ شود.
4. trust/revocation snapshot امضاشده و نسخه‌دار تولید و از کانال‌های آنلاین و انتقال آفلاین توزیع شود.
5. نصب آفلاین تا دریافت trust update جدید باید با سیاست fail-closed عمل کند؛ عدم دریافت update نباید به معنی پذیرش خودکار کلید revoke‌شده باشد.
6. بازهٔ احتمالی compromise، artifactهای صادرشده، درخواست‌های pending و audit eventها بررسی شوند؛ هیچ payload یا private key در گزارش incident درج نشود.
7. بازیابی سرویس فقط پس از تأیید دو نفره، سلامت HSM، صحت trust store و آزمون sign/verify مجاز است.

## 9. Backup، Recovery و Disaster Recovery

### HSM Vendor

- Backup فقط با روش رسمی و پشتیبانی‌شدهٔ همان سازنده، داخل مرز امن HSM و تحت کنترل دو نفر انجام شود.
- اگر سازنده backup رمز‌شده/شکافتن دانش/کворوم را پشتیبانی می‌کند، تعداد Custodianها، محل رسانه، کنترل دسترسی، آزمون بازیابی و تاریخ انقضا ثبت شود.
- private key به PFX/PEM/DER یا فایل قابل‌حمل export نشود؛ اگر backup procedure نیازمند خروج private key به فایل باشد، آن روش برای این سامانه ممنوع است.
- Backup رمز‌شده در محل جدا از HSM اصلی و با دسترسی دو نفره نگهداری شود. رسانه و دسترسی‌ها دوره‌ای بازبینی شوند.
- Restore فقط در محیط بازیابی ایزوله و با دو Custodian انجام شود؛ پس از restore، fingerprint public key، KeyId، صفات non-extractable، policy و audit chain بررسی شود.
- اگر HSM/backup قابل بازیابی نیست، کلید جدید تولید و rotation/revocation انجام شود؛ کلید گمشده با software fallback جایگزین نشود.

### TPM/OS secure store مشتری

- private key غیرقابل‌استخراج TPM به‌صورت فایل backup نمی‌شود. روش recovery، re-enrollment نصب با کلید تازه است.
- از دیتابیس/فایل مشتری فقط public key، KeyId، metadata enrollment و trust state قابل backup هستند؛ این داده‌ها private key محسوب نمی‌شوند.
- restore یک backup قدیمی نباید باعث اعتماد مجدد به KeyId revoke‌شده شود؛ trust/revocation anchor باید از مسیر مستقل و نسخه‌دار به‌روز شود.

## 10. Health، alerting و پاسخ به Provider outage

- endpoint مانیتورینگ Vendor: /health/signing. وضعیت unhealthy یا HTTP 503 باید به alert عملیاتی تبدیل شود.
- مانیتورینگ باید availability، latency، timeout، خطای login، نبود token، نبود key، mismatch manufacturer/model، unsupported mechanism و شکست audit append را ثبت کند.
- alertها فقط شناسهٔ خطا، purpose، KeyId عمومی، correlation و زمان را نشان دهند؛ PIN، payload، signature و exception message خام ممنوع‌اند.
- در outage، هیچ امضای cache‌شده، software fallback یا کلید تستی استفاده نشود. درخواست صدور در وضعیت retry-safe باقی بماند و API پاسخ عمومی 503 بدهد.
- پس از بازگشت Provider، health probe، sign/verify تستی کنترل‌شده، audit append و وضعیت trust بررسی شود؛ سپس صدور با تأیید مسئول مجاز از سر گرفته شود.
- پایش /health/signing باید توسط ابزار مانیتورینگ مستقل استقرار انجام شود؛ خود endpoint به‌تنهایی notification ارسال نمی‌کند.

## 11. شواهد و فرم صورت‌جلسه

برای هر Ceremony/rotation/revoke/recovery یک رکورد غیرمحرمانه نگهداری شود:

- شناسهٔ Ceremony و Issue/Change request
- Purpose (package/license/customer-ack)، KeyId و نسخه
- نوع/مدل/firmware/token serial یا installation reference
- fingerprint public key و hash تنظیمات trust store
- صفات کلیدی تأییدشده (بدون مقادیر محرمانه)
- هویت دو Custodian، Maker و Checker و زمان UTC
- نتیجهٔ آزمون sign/verify و audit correlation
- تاریخ فعال‌سازی، پایان overlap و تاریخ revoke برنامه‌ریزی‌شده
- نتیجهٔ backup/restore drill و محل نگهداری امن آن (بدون secret)

**هرگز** PIN، private key، token backup secret، payload کامل، signature یا فایل حاوی key material را به صورت‌جلسه پیوست نکنید.

## 12. وضعیت اجرایی فعلی

- HSM واقعی خریداری/اجاره نشده و بودجهٔ فعلی صفر است؛ بنابراین Vendor Production package/license signing باید disabled و fail-closed بماند.
- SoftHSM فقط در محیط اختصاصی Testing و CI برای تست adapter استفاده می‌شود؛ هیچ token نرم‌افزاری در Development، Staging یا Production مجاز نیست.
- مدل و سازندهٔ HSM، محل فیزیکی، HA/DR و قرارداد پشتیبانی هنوز انتخاب نشده‌اند؛ در نتیجه بخش‌های HSM این Runbook فعلاً **قابل اجرا نیستند**.
- Customer ACK production signing نیز تا پیاده‌سازی و اثبات backend غیرقابل‌استخراج TPM/OS و trust enrollment آفلاین فعال نمی‌شود.
- این سند راهنمای عملیاتی است؛ اجرای آن بدون HSM واقعی، دو Custodian و تأیید Security Owner مجاز نیست.

## 13. Customer offline-revocation TPM NV Extend index (D-057 / Issue #364)

این NV index با کلید امضای ACK یا کلید رمزنگاری/امضای TPM مشتری یکی نیست. کاربرد آن فقط ثبت hash-chain غیرقابل بازگشت برای وضعیت ابطال آفلاین است.

### 13.1 مشخصات index و مالکیت

- نوع NV index: TPM_NT_EXTEND، الگوریتم نام/هش SHA-256، اندازهٔ NV برابر 32 بایت.
- مقدار اولیه باید 32 بایت صفر باشد؛ runtime در صورت مقدار اولیهٔ متفاوت، وضعیت را Unavailable می‌کند و مجاز به reset یا redefine نیست.
- Runtime فقط مجوز Read و Extend روی همان index را دریافت می‌کند. مجوز Owner/Platform برای Define/Undefine/ChangeAuth فقط در Ceremony اپراتور مجاز باقی می‌ماند.
- Authorization باید با TPM policy مناسب به هویت سرویس محدود شود؛ PIN/auth value در command line، appsettings، Git، log یا telemetry قرار نمی‌گیرد. تحویل secret از Windows Credential Manager/DPAPI سازمانی یا systemd credentials/secret manager انجام می‌شود.
- NV index باید از هر backup دیتابیس مشتری مستقل باشد. Backup دیتابیس شامل envelope و anchor digestهاست، نه NV state یا TPM authorization.

### 13.2 Ceremony تعریف index

1. دو Custodian مجاز هویت TPM، firmware، فعال‌بودن TPM 2.0، SHA-256 و دسترسی runtime را ثبت کنند.
2. index با handle رزروشده و ثبت‌شده در inventory سازمان تعریف شود؛ انتخاب handle نباید با indexهای موجود تداخل داشته باشد.
3. TPM_NT_EXTEND، SHA-256 و dataSize=32 و policyهای Read/Extend/عدم امکان حذف توسط runtime بررسی شوند.
4. یک مقدار آزمایشی غیرعملیاتی Extend شود؛ read-back باید دقیقاً با SHA-256 مقدار قبلی به‌علاوه extension data برابر باشد.
5. index عملیاتی فقط با مقدار صفر provision شود. پس از پذیرش اولین snapshot، هرگز به صفر برگردانده یا دوباره تعریف نمی‌شود.
6. handle، policy digest، fingerprint عمومی TPM، مدل/firmware، Custodianها و نتیجهٔ آزمون ثبت شود؛ هیچ auth value یا secret ثبت نشود.

### 13.3 Runtime و Recovery

- Windows: adapter از TSS.MSR TbsDevice و TPM Base Services (TBS) استفاده می‌کند.
- Linux: StrictLinuxTpmDevice مستقیماً به /dev/tpmrm0 یا /dev/tpm0 متصل می‌شود؛ از LinuxTpmDevice کتابخانه استفاده نمی‌شود چون fallback به TCP simulator دارد.
- در هر دو پلتفرم هیچ TCP simulator، device fallback یا software fallback مجاز نیست.
- پیکربندی runtime از Licensing:OfflineRevocation:Tpm خوانده می‌شود: NvIndexOffset، NvIndexAuthValueBase64 و LinuxDevicePath. مقدار AuthValue فقط از secret injection محیط استقرار وارد شود؛ در appsettings/Git ثبت نشود.
- هر عملیات Extend باید current digest را بخواند، مقدار مورد انتظار را کنترل کند، دقیقاً یک NV_Extend انجام دهد و مقدار را مجدداً بخواند.
- Crash قبل از Extend با pending معتبر و TPM digest قبلی recover می‌شود؛ crash پس از Extend با TPM digest مورد انتظار finalize می‌شود. هر مقدار دیگر، missing pending، mismatch یا خطای TPM fail-closed است.
- TPM Clear، تعویض سخت‌افزار، حذف index یا فقدان authorization با تعریف index جدید و restore دیتابیس حل نمی‌شود. نصب باید وارد فرایند recovery/re-enrollment کنترل‌شده شود.
- قبل از Production، آزمون فیزیکی Windows 10/11، Windows Server و Linux، power-loss در هر crash window، restore backup قدیمی، TPM clear و عدم امکان Undefine توسط Runtime الزامی است.

### 13.4 وضعیت فعلی

- D-057 پذیرفته شده و migration/state store customer-side در شاخهٔ Issue #364 در حال پیاده‌سازی است.
- قرارداد NV Extend و default provider unavailable/fail-closed ثبت شده است.
- Adapter با Microsoft.TSS پیاده‌سازی شده است: Windows از TBS و Linux از device محلی strict استفاده می‌کند؛ LinuxTpmDevice کتابخانه استفاده نمی‌شود چون امکان fallback به simulator دارد. Runtime هیچ مسیر TCP یا simulator ندارد.
- NV index provisioning ceremony اجرایی و تست روی TPM سخت‌افزاری هنوز NotImplemented / NotVerified هستند. تا آن زمان هیچ نصب Production نباید این کنترل را فعال یا ادعای anti-rollback سخت‌افزاری کند.