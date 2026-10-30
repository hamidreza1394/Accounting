# D-055 — سرویس امضای مستقل HSM/TPM با PKCS#11 برای Vendor و ACK مشتری

- **وضعیت: NeedsApproval.** این سند پیش‌نویس معماری است؛ تا پاسخ مالک به سه پرسش انتهایی و تأیید نهایی، کد Production برای امضا نوشته نمی‌شود.
- **دامنه:** امضای بستهٔ Provisioning، لایسنس Vendor و acknowledgement نصب مشتری. هر دامنه کلید مستقل دارد؛ اشتراک کلید یا KeyId ممنوع است.
- **مبنای مستقل از برند ابری:** سرویس امضای اختصاصی تحت کنترل ما، پشت interfaceهای امضای موجود، با اتصال HSM از طریق استاندارد PKCS#11. Azure Key Vault و AWS KMS صرفاً Adapter اختیاری آینده‌اند و بدون تأیید جداگانه ساخته نمی‌شوند.
- **Vendor — بسته و لایسنس:** سرویس امضای مرکزی کنار Vendor اجرا می‌شود. کلید بسته و کلید لایسنس در HSM و غیرقابل‌استخراج‌اند و از یکدیگر جدا هستند. Maker-Checker و تأیید دو نفرهٔ موجود حفظ می‌شود؛ هیچ اپراتوری به private key دسترسی مستقیم ندارد.
- **Customer ACK:** هر نصب کلید اختصاصی غیرقابل‌استخراج خود را در TPM یا ذخیره‌ساز امن سیستم‌عامل همان نصب ایجاد/نگهداری می‌کند. کلید عمومی در Trust Store مشتری و Vendor ثبت می‌شود. امضای ACK هنگام اجرا کاملاً محلی است و به اینترنت، Vendor یا cloud وابسته نیست.
- **قرارداد رمزنگاری:** ECDSA P-256 مطابق قرارداد فعلی و امضای بایت‌های canonical دقیق. KeyId نسخه‌دار، یکتا و غیرقابل استفادهٔ مجدد است. private key هرگز خارج از HSM/TPM نمی‌آید و در Git، log، artifact، package یا configuration قرار نمی‌گیرد.
- **چرخهٔ کلید:** کلیدهای package/license/ACK مجزا؛ rotation با overlap در Trust Store؛ امکان revoke هر KeyId و رد fail-closed کلید لغوشده؛ نگهداری وضعیت و بازهٔ اعتبار نسخه‌ها.
- **عملیات:** Key Ceremony مستند و ممیزی‌پذیر؛ کنترل دو نفره برای عملیات حساس؛ Backup/Recovery با روش پشتیبانی‌شدهٔ HSM/TPM بدون export کلید خصوصی؛ رویهٔ جایگزینی HSM/TPM و بازیابی نصب؛ audit هر عملیات امضا فقط با metadata غیرحساس (purpose، KeyId/version، result، correlation، timestamp) و بدون payload/signature/key material.
- **Fail-closed:** نبودن، خرابی، timeout یا عدم دسترسی Provider یعنی امضا انجام نمی‌شود و API پاسخ عمومی 503 می‌دهد. هیچ software fallback، کلید آزمایشی یا cache امضای قابل استفاده در Production مجاز نیست.
- **سلامت و هشدار:** readiness/health، telemetry و alert برای availability، latency و خطای Provider؛ بدون افشای endpoint، credential یا key material.
- **تست:** SoftHSM فقط برای تست محلی و CI؛ هرگز Provider مجاز Production نیست. پوشش لازم: ECDSA P-256، عدم استخراج private key، تفکیک purpose/key، KeyId versioning، rotation overlap، revocation، unavailable/timeout/503، audit بدون دادهٔ حساس و ACK آفلاین.
- **ترتیب اجرا پس از تصویب:** ۱) ADR نهایی پس از پاسخ مالک؛ ۲) Adapter PKCS#11 و تست‌ها؛ ۳) Runbook عملیاتی HSM/TPM، Key Ceremony، rotation/revocation، backup/recovery، incident response و alerting. هر مرحله PR کوچک مستقل.
- **مواردی که فعلاً انتخاب نمی‌شوند:** سازنده/مدل HSM، مکان فیزیکی، HA/DR، قرارداد پشتیبانی، استقرار سرویس و هر cloud adapter.
- **پرسش‌های الزامی مالک پیش از ADR نهایی و کد Production:**
  1. Vendor دقیقاً کجا میزبانی خواهد شد (سرور اختصاصی خودمان، دیتاسنتر، cloud مشخص یا ترکیبی)؟
  2. آیا دسترسی و مجوز استفاده از حساب Azure یا AWS وجود دارد؟ (این پاسخ فقط برای بررسی Adapter اختیاری است و هیچ برندی را مبنا نمی‌کند.)
  3. بودجهٔ HSM برای خرید/اجاره، HA و پشتیبانی سالانه چقدر است؟
- **وضعیت فعلی:** interfaceهای امضا و providerهای unavailable/fail-closed موجود حفظ می‌شوند. تا پاسخ هر سه پرسش و تأیید نهایی ADR، هیچ PKCS#11 adapter، سرویس امضای Production، کلید، secret، migration یا تنظیم استقرار Production اضافه نمی‌شود.
- **مراجع:** D-046 تا D-054؛ Issues #233، #235، #236؛ docs/handoffs/ISSUE-236-provisioning-maker-checker.md و docs/LICENSING_THREAT_MODEL.md.
