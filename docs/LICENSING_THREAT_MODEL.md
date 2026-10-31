# مدل تهدید لایسنس (طرح هدف)

> **وضعیت بازبینی‌شده در 2026-10-29:** این سند ترکیبی از کنترل‌های هدف، کنترل‌های پیاده‌سازی‌شده و موانع عملیاتی است؛ بنابراین جملهٔ کلی «هیچ‌یک پیاده‌سازی نشده‌اند» معتبر نیست. در Vendor، بخشی از کنترل‌های maker-checker، ثبت تراکنشی پاکت لایسنس، قرارداد canonical امضا، جداسازی interface امضای provisioning، زنجیرهٔ ممیزی و guard مربوط به checkpoint پیاده‌سازی شده‌اند و برای بخش‌های مربوط CI وجود دارد. بااین‌حال signer واقعی KMS/HSM، مصرف‌کننده و verifier سمت مشتری، پروتکل ابطال آفلاین، مقصد مستقل checkpoint و آزمون بازیابی Production هنوز کامل نیستند؛ سامانه **Production-ready نیست** و provider پیش‌فرض امضا عمداً fail-closed است. این سند ادعای امنیت مطلق نمی‌کند؛ در نصب کاملاً آفلاین، کاربر دارای کنترل کامل ماشین می‌تواند محدودیت‌های سمت مشتری را دور بزند.

## دارایی‌ها
کلید خصوصی صدور؛ فایل لایسنس؛ کلید نصب؛ ممیزی؛ داده مالی مشتری؛ دیتابیس فروشنده.

## تهدیدها و کنترل‌ها

| # | تهدید | کنترل طرح‌شده | ریسک باقی‌مانده |
|---|---|---|---|
| T1 | جعل لایسنس | امضای نامتقارن؛ کلید خصوصی فقط نزد فروشنده (D-024) | افشای کلید خصوصی |
| T2 | دستکاری فایل | امضا روی بایت‌های دقیق؛ رد در صورت اختلاف | — |
| T3 | استفاده از لایسنس نصب دیگر | `installationId` و اثرانگشت کلید در Payload | کپی کامل ماشین |
| T4 | تکرار/جایگزینی با لایسنس قدیمی | `requestId`، `sequence` یکنواخت | حذف کامل دیتابیس نصب |
| T5 | عقب‌گرد ساعت | ثبت آخرین زمان مشاهده‌شده، تشخیص و ثبت (سیاست `NeedsApproval`) | ساعت و بازگردانی کامل ماشین |
| T6 | کپی ماشین مجازی (آفلاین) | **قابل جلوگیری نیست**؛ قرارداد و بازرسی فروشنده | پذیرفته‌شده |
| T7 | دور زدن در UI | بررسی سمت سرور در API؛ React فقط بازتاب | — |
| T8 | پاچ کردن باینری مشتری | امضای فایل‌های منتشرشده، تشخیص دستکاری (طراحی آتی) | مشتری با کنترل کامل |
| T9 | افشای کلید خصوصی | HSM/Secret Store، چرخش `kid`، فهرست ابطال کلید | بازه افشا |
| T10 | سوءاستفاده مدیر فروشنده | هویت جدا، MFA، ممیزی غیرقابل‌تغییر، اصل حداقل دسترسی | — |
| T11 | ورود کلید خصوصی به Git/بسته | اسکن Git و خروجی Publish در CI | — |
| T12 | حمله به سامانه فروشنده و دسترسی به داده مشتری | داده مالی مشتری در دیتابیس فروشنده نیست (D-020/D-021) | داده تجاری مشتری |
| T13 | حساب پشتیبان دائمی/دسترسی پنهان | ممنوع (D-029) | — |
| T14 | تمدید با سقف کمتر و حذف داده | داده حذف نمی‌شود؛ فقط ایجاد جدید محدود (D-022/D-026) | — |
| T15 | انکار صدور/تغییر | ممیزی رخدادها | — |

## فرض‌ها
فروشنده کلید خصوصی را در محیط امن نگه می‌دارد؛ نصب مشتری تحت کنترل مشتری است؛ مشتری قانونی با تعویض سخت‌افزار نباید قفل شود.

## سابقهٔ Issue #214 — کنترل‌های صدور لایسنس و وضعیت فعلی

- درخواست صدور/ابطال لایسنس از مسیر `/api/vendor/licenses/{id}/approval-request` با permission سمت سرور `vendor.licenses.issue`، بررسی مالکیت resource، اعتبارسنجی `RowVersion` و ثبت Audit درخواست می‌شود.
- درخواست در مدل Approval موجود ذخیره می‌شود و شاخص یکتای Pending مانع درخواست‌های هم‌زمان تکراری برای همان لایسنس است.
- تأیید لایسنس فعلاً به‌صورت صریح fail-closed است (`503 license_issuance_unavailable`): metadata به‌تنهایی لایسنس امضاشده نیست و endpoint حق ندارد وضعیت `Issued` یا `Revoked` را بدون signer و ذخیره‌سازی پایدار artifact تغییر دهد. رد درخواست برای بستن امن درخواست همچنان قابل انجام است.
- این بخش یادداشت تاریخی است؛ persistence envelope، کنترل‌های تراکنشی و تست‌های SQL Server در PRهای بعدی به main افزوده شده‌اند. موارد Production باقی‌مانده در Issue #214 و بخش‌های پایین‌تر همین سند پیگیری می‌شوند. هیچ کلید خصوصی در مخزن، CI یا بسته قرار نمی‌گیرد.

## آزمون‌های پذیرش آتی
امضای نامعتبر؛ `kid` ناشناخته؛ نصب دیگر؛ `requestId` تکراری؛ `sequence` قدیمی؛ فایل دستکاری‌شده؛ انقضا/ارفاق/محدودشده؛ عدم وجود کلید خصوصی در بسته Publish؛ عدم دسترسی ثبت در حالت محدودشده؛ مسیرهای دور زدن مستقیم URL.

## سابقهٔ طرح اولیهٔ فاز صفر — 2026-10-24

> این جدول، فهرست تهدیدها و کنترل‌های پیشنهادی در زمان تدوین اولیه است؛ برچسب‌های این بخش وضعیت جاری پیاده‌سازی را مشخص نمی‌کنند. برای وضعیت واقعی هر کنترل، شواهد PR/CI و موانع جاری در Issueهای مرتبط (#107، #214، #228، #235 و #236) ملاک است.

### تهدیدهای افزوده

| # | تهدید | کنترل طرح‌شده | ریسک باقی‌مانده |
|---|---|---|---|
| T16 | دورزدن سقف شرکت/زیرسیستم با تغییر مستقیم درخواست API، فراخوانی مستقیم URL یا درخواست هم‌زمان | یک نقطهٔ سیاست سمت سرور؛ شمارش از DB؛ تراکنش؛ آزمون ماتریسی (D-025؛ `LICENSING_WORKFLOWS.md` بخش ۱۳) | مسیرهایی که از نقطهٔ سیاست عبور نمی‌کنند؛ تا ورود ماژول‌های حسابداری فقط ایجاد شرکت آزمون‌پذیر است |
| T17 | استفاده از فایل لایسنس معتبر روی نصب دیگر | `installationId` و اثرانگشت کلید در Payload | کپی کامل ماشین با کلید (T6) |
| T18 | سرقت کلید خصوصی نصب از Backup یا دیسک | رمزگذاری Backup با عبارت عبور مدیر؛ حفاظت کلید (OD-08) | در Linux/macOS حلقهٔ کلید Data Protection فعلی در سطح کلید رمزنگاری نمی‌شود (فقط DPAPI در Windows؛ `DatabaseConnectionSettingsStore.cs`) |
| T19 | عقب‌گرد ساعت برای تمدید ظاهری لایسنس مدت‌دار | ساعت مؤثر `max(...)` و ممیزی (بخش ۱۰ گردش‌کارها) | بازگردانی کامل ماشین/دیتابیس |
| T20 | نفوذ به دیتابیس یا میزبان فروشنده و صدور لایسنس جعلی | سرویس امضا جدا از رابط مدیریت؛ کلید در HSM/KMS؛ ممیزی زنجیره‌ای؛ تفکیک وظایف (D-043) | مدیر داخلی با دسترسی کامل به سرویس امضا |
| T21 | نشت کلید خصوصی از طریق Git، CI یا بستهٔ Publish | G1–G5 در D-041؛ اسکن اسرار؛ امضا در Actions انجام نمی‌شود | خطای انسانی در محیط فروشنده |
| T22 | ورود کد فروشنده یا پنل مدیریتی به بستهٔ مشتری (زنجیرهٔ تأمین) | گارد وابستگی G1 و بررسی خروجی G2 | پیش از پیاده‌سازی گاردها: بدون کنترل خودکار |
| T23 | سوءاستفاده از Endpoint آنلاین: Replay، شمارش شناسه‌ها، DoS | اثبات تملک کلید، `requestId` یک‌بارمصرف، Rate Limit، پاسخ خطای یکنواخت، سقف بدنه (D-042) | DoS توزیع‌شده در لایهٔ شبکه |
| T24 | تغییر یا حذف رخداد ممیزی فروشنده برای انکار صدور/ابطال | Append-only، زنجیرهٔ هش، نقاط بازرسی خارج از سرور (D-043) | مهاجم با کنترل کامل روی همهٔ نسخه‌های بازرسی |
| T25 | بازگشت به نسخهٔ قدیمی برنامه که کنترل لایسنس ندارد | قرارداد؛ ثبت نسخه در درخواست آنلاین؛ پشتیبانی/به‌روزرسانی مشروط | **غیرقابل مهار آفلاین** |
| T26 | فایل ورودی بزرگ یا مخرب در ورود لایسنس | سقف اندازه؛ تجزیهٔ امن؛ سربرگ ثابت‌اندازهٔ کوچک (`formatVersion`، `kid`، طول) با سقف اندازه پیش از تأیید امضا تجزیه می‌شود و بدنه پس از تأیید امضا؛ fail-closed | — |
| T28 | خاموش‌کردن اعمال با تنظیم محلی (`EnforcementMode=Off/Observe`) یا ماندن روی نسخهٔ قدیمی | `Enforce` حداقل ثابت در کد هر نسخه؛ پایین‌آوردن فقط با لایسنس امضاشده (D-044) | **غیرقابل مهار برای نسخه‌های پیش از اعمال و نسخه‌های قدیمی**؛ قرارداد |
| T29 | افزودن کلید عمومی مهاجم به جدول `TrustedSigningKey` با دسترسی نوشتن به دیتابیس مشتری | کلید ریشهٔ عمومی داخل برنامه؛ ردیف‌های جدول فقط «بستهٔ اعتماد» امضاشده توسط ریشه‌اند و بدون امضای ریشه نادیده گرفته می‌شوند (OD-07) | مشتری با کنترل کامل روی باینری (T8) |
| T30 | پرش بزرگ ساعت به جلو و قفل دائمی لایسنس مدت‌دار | سقف پیشروی «ساعت مؤثر» در هر ارزیابی؛ پرش بزرگ فقط با تأیید مدیر؛ لایسنس تازهٔ امضاشده آن را بازنشانی می‌کند (بخش ۱۰ گردش‌کارها) | — |
| T27 | قفل‌شدن مشتری قانونی با خرابی ساعت، تعویض سخت‌افزار یا خطای فایل | حالت محدود غیرمخرب؛ خروجی/پشتیبان همیشه مجاز؛ اتصال مجدد (D-026، D-027) | زمان پاسخ فروشنده |

### تهدیدهایی که در حالت آفلاین قابل کشف یا مهار کامل **نیستند**

- کپی کامل ماشین مجازی/دیتابیس به‌همراه کلید نصب (T6، T17).
- بازگردانی کامل ماشین به زمان قبل برای بازگرداندن «ساعت مؤثر» و وضعیت لایسنس (T5، T19).
- اجرای نسخهٔ قدیمی‌تر برنامه که اعمال لایسنس ندارد (T25).
- ابطال یا تعلیق فوری لایسنس نصب بدون ارتباط (D-023 بند ۷).
- نگه‌داشتن `EnforcementMode` روی `Off/Observe` یا ماندن روی نسخهٔ پیش از اعمال (T28).
- پاچ‌کردن باینری یا دور زدن اعمال سمت مشتری توسط مشتری با کنترل کامل روی ماشین (T8).
- ادعای «مصونیت مطلق» در هیچ‌جای مستندات یا رابط کاربری مجاز نیست (`AGENTS.md` ۵.۲).

### الزامات امنیتی طراحی (برای PRهای آینده)

- **S-01:** اعتبارسنجی fail-closed: قالب یا `kid` ناشناخته، امضای نامعتبر، `installationId` ناسازگار، `sequence` قدیمی یا `requestId` تکراری هرگز لایسنس معتبر نمی‌سازد و وضعیت قبلی را حفظ می‌کند.
- **S-02:** اعمال لایسنس و زیرسیستم فقط سمت سرور و از طریق یک نقطهٔ سیاست؛ React فقط بازتاب است.
- **S-03:** مجوز عملیاتی کاربر و لایسنس تجاری مستقل‌اند و هر دو لازم‌اند (D-025).
- **S-04:** هیچ کلید خصوصی، رمز یا داده حسابداری در پاکت، درخواست، Log، ممیزی یا گزارش نیست.
- **S-05:** کلید خصوصی صدور خارج از مخزن/بسته/CI و با دسترسی فقط-امضا (D-041، OD-07).
- **S-06:** مدیران مرکزی با MFA، نقش حداقل و ممیزی مقاوم در برابر دست‌کاری (D-043).
- **S-07:** Rate Limit و ضد-Replay در Endpointهای آنلاین (D-042). Rate Limit فعلی نصب تک‌نمونه‌ای است؛ Endpoint فروشنده به ذخیرهٔ مشترک نیاز دارد.
- **S-08:** انقضا/انتقال/ابطال هرگز داده حسابداری را حذف یا تغییر نمی‌دهد (D-026).
- **S-09:** منبع زمان یکدست (`TimeProvider`) و ساعت مؤثر برای ارزیابی (OD-05).
- **S-10:** گارد ورود کد/کلید فروشنده به بستهٔ مشتری (G1–G3 در D-041) پیش از اولین کد امضا.

### آزمون‌های پذیرش افزوده

دورزدن مستقیم URL و درخواست هم‌زمان برای سقف شرکت؛ فایل لایسنس بزرگ/مخرب؛ عقب‌گرد ساعت با و بدون بازگردانی؛ نبود هر اسمبلی فروشنده و هر کلید خصوصی در خروجی Publish؛ شکست گارد با ارجاع آزمایشی؛ قطع شبکه حین تمدید آنلاین بدون تغییر وضعیت؛ دست‌کاری رخداد ممیزی فروشنده و کشف آن؛ ورود بدون MFA برای مدیر مرکزی.

## وضعیت اجرایی ممیزی Vendor — 2026-10-25
- Issue #105 شاخه‌ای برای Writer/Verifier زنجیرهٔ ممیزی اضافه می‌کند: hash نسخه‌دار SHA-256 روی شناسه‌های غیرحساس و لینک به رخداد قبلی، قفل تراکنشی SQL Server و آزمون تشخیص دستکاری.
- تا CI/ادغام PR، کد `NotVerified` است و هنوز به Host متصل نشده است. نبود endpoint به معنای کامل‌بودن ممیزی نیست.
- کنترل‌های باقی‌ماندهٔ Production: حساب SQL فاقد مجوز UPDATE/DELETE روی رخدادها، checkpoint/hash head به storage مستقل با permission جدا، هشدار روی شکست verifier، retention و runbook بازیابی. مهاجم DBA با دسترسی کامل می‌تواند کل زنجیره را بازنویسی کند مگر checkpoint بیرونی وجود داشته باشد.


## قرارداد امضای Vendor — پیگیری Issue #214 (2026-10-28)

- `ILicenseSigningProvider` یک مرز provider-neutral در `Vendor.Host` تعریف می‌کند: ورودی، بایت‌های دقیق UTF-8 payload canonical است؛ خروجی موفق باید `kid`، الگوریتم allowlist‌شدهٔ D-045 و امضای ECDSA P-256 با قالب IEEE P1363 ثابت ۶۴ بایتی داشته باشد.
- قرارداد اجازه نمی‌دهد پیاده‌سازی payload را deserialize و دوباره serialize کند؛ هر تغییر حتی whitespace باید امضا را نامعتبر کند. تست از کلید ECDSA موقت و فقط در حافظهٔ تست استفاده می‌کند.
- این commit **هیچ** provider تولیدی، کلید نرم‌افزاری fallback، endpoint صدور، ذخیره‌سازی envelope، Migration یا تغییر در `Server/**` اضافه نمی‌کند. endpoint فعلی همچنان fail-closed است.
- پیش از اتصال provider: انتخاب KMS/HSM یا سرویس امضای مورد تأیید، احراز هویت workload، محدودیت عملیات فقط-امضا، timeout/retry امن، rotation و retirement کلیدها، ثبت `kid` و تست end-to-end لازم‌اند. کلید خصوصی نباید در برنامه، repo، CI یا artifact قرار گیرد.


## Provisioning signing-key separation — Issue #233 (2026-10-28)

- Provisioning packages use the dedicated `IProvisioningSigningProvider` boundary; license issuance uses `ILicenseSigningProvider`. The registrations and key responsibilities must remain separate even when both contracts use the same ECDSA P-256 signature format.
- The default provisioning provider is `UnavailableProvisioningSigningProvider`, which returns an unavailable result and honors cancellation. It does not create, persist, log, or package private-key material.
- This is an architectural boundary, not a production signer. Production remains blocked until an approved external KMS/HSM or protected signing service uses a dedicated provisioning key, resolves key IDs from trusted server-side configuration, verifies signatures before persistence, and has documented rotation/recovery and operational tests. No provider-specific implementation is selected by this change.


## Provisioning signer timeout boundary (Issue #236, 2026-10-29)

- Vendor issuance now bounds waiting for the external provisioning signer to 15 seconds and passes a linked cancellation token to the provider.
- Timeout or signer-side cancellation maps to the same generic unavailable result; the issuance transaction does not persist a package or transition the request to `PendingDelivery` on this result.
- Cancellation initiated by the HTTP/request caller remains cancellation and is not converted into a signer outage.
- The default provider remains unavailable/fail-closed. This is timeout protection, not a KMS/HSM adapter, and does not make Production signing available.


## Customer provisioning consumer and operator enrollment — D-053 / Issue #235 (2026-10-30)

- **مرجع شرکت مادر:** ParentCompanyReference یک GUID canonical با قالب D و همان Vendor-issued RequestId بستهٔ provisioning مادر است. تنها resolve مجاز RequestId → ProvisioningRequestClaim → CompanyId است؛ هیچ fallback بر اساس نام، NationalId یا متن آزاد وجود ندارد.
- **تفکیک کنترل توزیع‌شده:** Vendor پیش از صدور/امضا، مادر را در همان Customer و Installation و در وضعیت Confirmed پس از acknowledgement امضاشده بررسی می‌کند. Customer در زمان apply، claim مادرِ قبلاً اعمال‌شده، مرز دقیق Customer/Installation/CompanyBoundary و فعال‌بودن شرکت را کنترل می‌کند. نوع مجاز والد parent یا subsidiary است؛ independent رد می‌شود. هر mismatch کل بسته را رد می‌کند.
- **عدم ساخت گروه:** فقط ParentCompanyId در claim فرزند ثبت می‌شود؛ هیچ CorporateGroupMember یا OwnershipRelationship و درصد مالکیتی به‌طور خودکار ساخته نمی‌شود.
- **شرکت قدیمی:** نبود claim به معنی رد بسته است. افزودن شناسهٔ مرجع یکتا به Company خارج از D-053 و نیازمند ADR/تأیید مستقل است؛ این تصمیم هیچ schema جدیدی برای Company مجاز نمی‌کند.
- **تحویل Enrollment:** token تصادفی فقط در پاسخ apply پس از commit و فقط یک بار به اپراتور نصب مجاز برگردانده می‌شود؛ فقط hash در DB ذخیره می‌شود. پاسخ no-store است و token نباید در log، audit، acknowledgement، URL، telemetry یا cache ظاهر شود. replay فقط acknowledgement را برمی‌گرداند.
- **بازیابی:** از دست رفتن پاسخ با retry حل نمی‌شود. اپراتور مجاز باید از endpoint مستقل reissue استفاده کند که در تراکنش token قبلی را باطل، digest جدید و expiry کوتاه ثبت و audit بدون secret تولید کند. reissue باید rate-limited و محدود به اپراتور میزبان باشد.
- **ابر مشترک:** اپراتور نصب کارکنان میزبان است، نه مشتری. Enrollment token پس از مصرف، دسترسی یا عضویت ماندگار برای اپراتور ایجاد نمی‌کند. مدیر اولیه رمز خود را تعیین می‌کند و اولین ورود تا تغییر رمز محدود است.
- **محدودیت تلاش:** مصرف token اتمیک و یک‌بارمصرف؛ تلاش ناموفق محدود و پس از سقف مشخص، token قفل/باطل می‌شود. شمارنده و وضعیت لازم باید با schema claim و concurrency token سازگار باشد.
- **Production signing:** فقط provider واقعی مورد تأیید Issue #236؛ provider پیش‌فرض unavailable و fail-closed می‌ماند. کلید آزمایشی یا software fallback در Production ممنوع است.
- **محدودیت تأیید:** این بخش ثبت تصمیم و threat requirements است، نه اثبات پیاده‌سازی. تا اجرای تست SQL Server، replay هم‌زمان، rollback، endpointهای apply/reissue و CI روی commit نهایی، کنترل‌ها NotVerified هستند.


## PKCS#11 signing adapter — Issue #236 / D-055 (2026-10-30)

- PR #344 در main، `ILicenseSigningProvider` و `IProvisioningSigningProvider` را به adapter مشترک PKCS#11 متصل کرده است؛ هر purpose دارای KeyId نسخه‌دار و private-key object مستقل است. اشتراک کلید ممنوع است.
- Adapter فقط `CKM_ECDSA` روی digest SHA-256 بایت‌های canonical را اجرا می‌کند و امضای ۶۴ بایتی IEEE P1363 را می‌پذیرد. private-key object باید `CKA_SENSITIVE=true`، `CKA_EXTRACTABLE=false`، `CKA_ALWAYS_SENSITIVE=true`، `CKA_NEVER_EXTRACTABLE=true` و `CKA_SIGN=true` داشته باشد و `CKA_EC_PARAMS` دقیقاً P-256 باشد.
- برنامه کلید تولید یا export نمی‌کند. رویهٔ Key Ceremony و توزیع public key در Runbook ادغام‌شدهٔ PR #341 مستند است؛ اجرای واقعی آن به تأمین HSM بستگی دارد. PIN فقط از secret/environment injection می‌آید و هرگز log نمی‌شود.
- PKCS#11 opt-in و پیش‌فرض خاموش است. فقط محیط اختصاصی Testing، SoftHSM را می‌پذیرد؛ در Development/Staging/Production، slot سخت‌افزاری و manufacturer/model دقیقاً allowlist‌شده الزامی است. با بودجهٔ فعلی صفر و نبود HSM واقعی، signerهای Production باید unavailable/fail-closed باقی بمانند.
- هر تلاش امضا audit metadata-only با purpose، KeyId نسخه‌دار و نتیجه ثبت می‌کند؛ payload/signature/private material ثبت نمی‌شوند. شکست audit نتیجه امضا را دور می‌اندازد. `/health/signing` برای monitoring بیرونی 503 می‌دهد وقتی provider unavailable است.
- CI در PR #344 با SoftHSM موفق است، اما این اثبات HSM واقعی یا HA نیست. اجرای Key Ceremony، rotation/revocation، recovery، workload identity و sign-only operator controls به HSM واقعی وابسته است؛ ACK key per-customer TPM/OS و enrollment آفلاین نیز هنوز adapter اجرایی ندارد.

## Customer acknowledgement signing-key revocation (D-055 / Issue #236)

- Customer-side ACK verification uses the independent public-key set `Provisioning:TrustedAcknowledgementKeys:<keyId>`; private keys remain in the per-installation Windows CNG/TPM or approved OS secure store.
- A key can be explicitly revoked without deleting its public key by setting `Provisioning:RevokedAcknowledgementKeys:<keyId>=true`. The customer trust store rejects revoked KeyIds even if the SPKI entry remains present; malformed non-null revocation values also fail closed rather than being interpreted as active trust.
- During rotation, configure the new versioned KeyId/SPKI first and leave it active while the old key remains active; after the Vendor trust set is updated and the cutover is approved, mark the old KeyId revoked. Vendor-side trust independently retains its existing `Revoked` flag.
- A revoked customer KeyId makes the customer's local post-signature verification fail closed; no acknowledgement is committed. No database migration or internet lookup is required.
- This is configuration-based revocation, not an independently rollback-resistant trust anchor. An administrator restoring old configuration or an old full-system backup may restore prior trust; operational anti-rollback remains a separate control and must be addressed before claiming tamper-resistant revocation.


## Offline revocation state rollback and database tampering (D-057 / Issue #364)

- **Threat:** restoring an older SQL Server backup can restore an older revocation sequence and revoked-license list; direct database tampering can also alter a cached list without changing its sequence.
- **Control:** the customer installation stores the accepted signed envelope and a SHA-256 chain digest in its own database. A TPM 2.0 NV index of type EXTEND, outside the database backup domain, stores the irreversible chain head. Each extension binds a versioned domain separator, next generation and exact signed-payload digest.
- **Crash protocol:** the database first commits a pending signed envelope and expected TPM digest; the TPM NV_Extend is then issued and read back; finally the database promotes the envelope to accepted state. Recovery only proceeds if the TPM value is exactly the prior digest or the expected next digest. Other values fail closed.
- **Read integrity:** every state read verifies the TPM chain head, recomputes the prior-digest/generation/payload-digest link, verifies the signed envelope with the dedicated revocation trust set, and compares sequence, expiry, digest and revoked IDs with the persisted columns.
- **Concurrency:** SQL Server session application lock serializes cross-process prepare/extend/finalize operations. Rowversion remains an additional database concurrency control.
- **Residual risks:** TPM clear/replacement, NV-index undefine, compromised platform authorization, malicious kernel/administrator with live TPM access, or loss of the TPM require controlled recovery/re-enrollment. This control does not claim protection against a fully compromised running host.
- **Status:** customer persistence and fake-anchor protocol tests are in progress. A Microsoft.TSS-based Windows TBS/Linux local-device adapter is implemented, and Linux simulator fallback is explicitly avoided. No physical TPM hardware test or NV index provisioning ceremony has yet been verified; the runtime default remains unavailable/fail-closed unless the TPM section is explicitly configured. Do not claim production anti-rollback until Windows and Linux backends, NV provisioning ceremony and physical TPM tests are complete.

## تهدیدهای افزوده — restart-safe activation و OS identity (D-058 / Issue #384)
- **T31 — درخواست مجدد لایسنس پس از restart:** persistence پایدار InstalledLicense و InstallationIdentity؛ startup فقط load + cryptographic revalidation؛ restart نباید مسیر import را اجرا کند.
- **T32 — جعل یا clone کردن OS identity:** MachineGuid به‌تنهایی کافی نیست؛ marker سیستم‌عامل باید با possession کلید نصب non-exportable در TPM/CNG یا provider امن bind شود. DB row یا مقدار ارسالی client مرجع اعتماد نیست.
- **T33 — نصب مجدد Windows با باقی ماندن سخت‌افزار:** تغییر OS-installation marker موجب reactivation می‌شود؛ update/servicing همان installation نباید reactivation ایجاد کند. False positive باید با تست Windows servicing کنترل شود.
- **T34 — حذف/restore DB برای برگرداندن activation قدیمی:** sequence، signed envelope، installation binding و concurrency بررسی می‌شوند؛ state گم/ناسازگار fail-closed و فقط از مسیر recovery مجاز OD-06/08 بازسازی می‌شود.
- **Cloud trust boundary:** مرجع license و entitlements فقط server-side است؛ کلاینت فقط نمایش می‌دهد. Cache احتمالی فقط server-controlled، versioned و با expiry/invalidation مشخص است. قطع ارتباط cloud با مرجع authoritative باید طبق availability policy صریح مدیریت شود و به مجوز ساختگی یا fail-open منجر نشود.
- **محدودیت:** TPM-backed identity نیز در برابر مالک/ادمین با کنترل کامل سیستم یا compromise باینری مصونیت مطلق نمی‌دهد؛ clone کامل VM/host و بازیابی کامل ماشین در حالت آفلاین همچنان محدودیت‌های پذیرفته‌شده‌اند.

- **T35 — downgrade پنهانی از TPM به Software KSP:** ProviderName صریح و allow-list شده؛ نبود TPM به Software fallback نمی‌کند. Software KSP فقط با flag تأیید صریح در provisioning command مجاز است؛ AssuranceLevel و رضایت باید در DB/audit ثبت شوند. SoftwareProtected به‌عنوان TPM/Hardware گزارش نمی‌شود و در برابر مدیر محلی مخرب یا clone کامل host ضمانت سخت‌افزاری ندارد.


## تهدیدهای جدید بر اساس D-059 (2026-10-31)

- **T36 — سرقت/نشت رمز bootstrap مدیر آفلاین:** رمز فقط در فرم/فرمان محلی Customer Host دریافت می‌شود و فقط hash استاندارد ASP.NET Core Identity ذخیره می‌شود. رمز، hash، token قابل استفاده مجدد یا credential در Vendor، provisioning/license package، audit detail، log و telemetry قرار نمی‌گیرد. bootstrap باید یک‌بارمصرف و اتمیک باشد؛ تا تکمیل آن login/session عادی ممنوع است.
- **T37 — دور زدن سقف Seat با درخواست هم‌زمان:** شمارش عضویت فعال و ایجاد عضویت باید در مرز تراکنشی/قفل مناسب انجام شود؛ UI check کافی نیست. Seat در سطح Company است و عضویت یک User در دو Company دو Seat مصرف می‌کند. کاهش Seat از نوع block-new است و عضویت موجود را حذف نمی‌کند.
- **T38 — کاهش Feature/Module و حذف داده:** entitlement limit کاهش‌یافته نباید منجر به delete یا update مخرب داده موجود شود. عملیات create/expand مسدود و over-limit برای مدیر قابل مشاهده باشد.
- **T39 — replay/downgrade در amendment:** amendment به همان LicenseId و Installation bind می‌شود؛ sequence باید strictly increasing و امضای payload روی بایت دقیق باشد. revision قدیمی، تکراری یا متعلق به Company/Installation دیگر رد و audit شود.
- **T40 — اختلاط authority مرکزی و محلی:** Cloud entitlement را server-side resolve می‌کند؛ Offline آخرین revision امضاشده پذیرفته‌شده را حفظ می‌کند. قطع شبکه مجوز معتبر را باطل نمی‌کند. Vendor هرگز مستقیماً به Customer DB وصل نمی‌شود.
- **T41 — داده مصرف آفلاین stale به‌عنوان real-time:** usage آفلاین تا sync بعدی stale است؛ Vendor UI باید LastSyncedAt/freshness را نمایش دهد و آن را به‌عنوان وضعیت زنده نشان ندهد.
- **T42 — تغییر سیاست با کاهش entitlement:** Seat reduction برابر block-new و feature limit reduction برابر منع create/expand است؛ هیچ کاهش entitlement مجوز پاک‌کردن داده یا deactivate خودکار membership نیست.
