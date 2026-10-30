# تحویل وظیفه — ISSUE-235-transactional-consumer

- **Issue:** [#235](https://github.com/hamidreza1394/Accounting/issues/235)
- **تاریخ بازبینی:** 2026-10-30
- **مبنای کد:** `main` @ `43d28253eb9f0f38ff25d0dfea143aeacdb06871`
- **وضعیت:** پیاده‌سازی اصلی consumer و E2E نسخه ۴ در main ادغام و CI-verified است؛ فعال‌سازی Production به signer خارجی مورد تأیید در #236 وابسته است.

## خلاصه نتیجه

Issue #235 دیگر در وضعیت «consumer پیاده‌سازی نشده» نیست. مسیر تست‌شده شامل اعتبارسنجی بسته، claim/replay، apply تراکنشی، Enrollment یک‌بارمصرف، acknowledgement امضاشده در تست و intake سمت Vendor است. Providerهای پیش‌فرض Production عمداً unavailable/fail-closed باقی مانده‌اند.

## PRهای ادغام‌شده

| PR | نتیجه |
|---|---|
| #302 | Trust store مشتری، verifier و migration/claim پایه |
| #305 | کاتالوگ permissionهای مجاز و fail-closed |
| #306 | primitive توکن Enrollment تصادفی و hash-only |
| #307 | apply تراکنشی، claim/replay، reissue، Enrollment و acknowledgement |
| #308 | منع ورود/نشست مدیر تا تکمیل Enrollment |
| #293 | دریافت و اعتبارسنجی acknowledgement امضاشده در Vendor |
| #294 | نمایش نتیجه acknowledgement در تاریخچه Provisioning |
| #318 | حفظ status عمومی برای JSON ناقص/بزرگ |
| #319 | binding بسته به یک لایسنس امضاشده و Sequence در schema v4 (D-054) |
| #329 | E2E دو میزبان آزمایشی، دو دیتابیس مستقل و انتقال فایل |

## رفتار امنیتی تأییدشده

- Customer trust store مستقل از metadata بسته است؛ کلید ناشناخته، امضای نامعتبر یا binding نادرست fail-closed می‌شود.
- claim یکتا برای RequestId/nonce و تراکنش Serializable مانع apply تکراری و ایجاد شرکت/مدیر تکراری است.
- ایجاد Company، FiscalPeriod، User، CompanyRole، Membership و مجوزهای دقیق زیرسیستم در مرز تراکنش Customer انجام می‌شود.
- Enrollment token تصادفی ۲۵۶بیتی است؛ فقط digest ذخیره می‌شود، انقضا و سقف تلاش دارد، مصرف یک‌باره و reissue ممیزی‌شده دارد و token در replay دوباره افشا نمی‌شود.
- مدیر اولیه تا تکمیل Enrollment اجازه ورود یا ایجاد نشست ندارد. رمز واقعی را مدیر در Enrollment تعیین می‌کند؛ پس از آن `MustChangePassword=false` است. بنابراین نباید این رفتار را «تغییر رمز اجباری پس از اولین ورود» توصیف کرد.
- Vendor فقط acknowledgement معتبر و منطبق با request/nonce/hash/customer/installation را می‌پذیرد؛ نتیجه‌های مجاز Applied/AlreadyApplied به Confirmed و rejection مجاز به Failed منتهی می‌شوند؛ وضعیت و Audit تراکنشی‌اند.

## شواهد CI

- **PR #307:** .NET CI [41010026198](https://github.com/hamidreza1394/Accounting/actions/runs/41010026198)، Vendor CI [41010026058](https://github.com/hamidreza1394/Accounting/actions/runs/41010026058)، Semgrep [41010026110](https://github.com/hamidreza1394/Accounting/actions/runs/41010026110) — همگی موفق.
- **PR #308:** .NET CI [41014003705](https://github.com/hamidreza1394/Accounting/actions/runs/41014003705)، Semgrep [41014003694](https://github.com/hamidreza1394/Accounting/actions/runs/41014003694) — موفق.
- **PR #329:** .NET CI [41052515115](https://github.com/hamidreza1394/Accounting/actions/runs/41052515115)، Vendor CI [41052515292](https://github.com/hamidreza1394/Accounting/actions/runs/41052515292)، Semgrep [41052515389](https://github.com/hamidreza1394/Accounting/actions/runs/41052515389) — موفق. .NET شامل هفت job موفق است.

## دامنه و محدودیت‌های E2E

- دو WebApplicationFactory/TestServer و دو دیتابیس SQL Server مستقل در یک test process اجرا می‌شوند؛ این آزمون دو ماشین فیزیکی یا دو OS process مستقل نیست.
- کلیدهای E2E در حافظه و فقط برای تست ساخته می‌شوند؛ هیچ کلید خصوصی Production در repository یا customer installation وجود ندارد.
- پس از تحویل بسته به Customer کاملاً آفلاین، تعلیق بعدی یا Sequence جدیدتر Vendor به‌صورت فوری قابل کشف نیست.
- MaxCompanies، تجمیع چند لایسنس و افزودن ماژول به شرکت موجود طبق D-054 NotImplemented هستند.

## موارد باز

1. **#236:** پیکربندی Provider واقعی KMS/HSM برای امضای license/provisioning/ack با workload identity، حداقل دسترسی، rotation/recovery و تست عملیاتی. Providerهای پیش‌فرض باید fail-closed بمانند.
2. **#214:** پروتکل revocation آفلاین و consumer ضد rollback، انتقال/بازیابی و تست عملیاتی.
3. **#107:** مقصد مستقل checkpoint ممیزی با هویت و credential جدا، retention/immutability، alerting و restore drill.
4. **Staging:** اجرای واقعی دو process/host، cloud/dedicated/offline، backup/restore و recovery پس از انتخاب providerها.

## مواردی که در این handoff اجرا نشده‌اند

- تست دو ماشین یا دو OS process مستقل.
- تست با KMS/HSM واقعی یا credential عملیاتی.
- تست revocation آفلاین و rollback-resistant consumer.
- تست عملیاتی backup/restore و disaster recovery.
- اجرای محلی جدید توسط این بازبینی؛ شواهد تست به CIهای لینک‌شده محدود است.

## مرجع

- D-053 و D-054 در `docs/DECISIONS.md`
- Issue #228، #235، #236، #214 و #107
- PRهای جدول بالا
- وضعیت تجمیعی در `docs/PROJECT_STATUS.md`
