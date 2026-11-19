# تحویل وظیفه — ISSUE-396-license-runtime-gate

- **Issue:** #396 (دروازهٔ شروع برنامه و Activation Wizard) — تصمیم‌های مالک: **D-060** ( Accepted، 2026-10-31)، **D-098** (انقضای Cloud)، **D-059 §7** (انقضای Term → restricted)، **D-026** (حالت محدودشده)، **D-025/D-074** (ترتیب کنترل)
- **Agent/مسئول:** GLM (مأموریت مالک 2026-11-19؛ اتصال License به Runtime فرانو)
- **تاریخ:** 2026-11-19
- **وضعیت:** انجام‌شده در کد و تست؛ فعال‌سازی enforcement تصمیم عملیاتی استقرار است (پیش‌فرض خاموش)

## هدف و معیارهای پذیرش
- [x] وضعیت License در زمان اجرا سمت سرور resolve می‌شود: `Active` / `Restricted` / `Unlicensed` / `Invalid` — فقط از Envelope امضاشده و اعتبارسنجی revocation-aware؛ هیچ ورودی کلاینت منبع تصمیم نیست.
- [x] Unlicensed: عملیات عادی (Login، APIها) بسته؛ فقط مسیرهای فعال‌سازی/بازیابی (`/api/v1/licensing/*`، `/api/v1/provisioning/*`) و خروج از نشست باز می‌مانند (D-060 §3).
- [x] Invalid (امضا/binding/revocation نامعتبر): fail-closed با `license_invalid`.
- [x] Restricted در Dedicated: خواندن (GET/HEAD/OPTIONS) باز، نوشتن عادی 403؛ داده حذف یا قفل نمی‌شود (D-059 §7، block-new، بدون grace ضمنی).
- [x] Restricted در Cloud: ورود عادی و عملیات عادی بسته با `license_cloud_expired`؛ مسیر تمدید/بازیابی (licensing/provisioning) باز (D-098)؛ هیچ داده‌ای حذف نمی‌شود.
- [x] endpoint وضعیت `GET /api/v1/licensing/status` در همهٔ حالت‌ها در دسترس است و projection فقط از payload امضاشده می‌سازد (kind، تاریخ‌ها، support/updates، maxCompanies، company entitlements).
- [x] کنترل سمت سرور است؛ مخفی‌کردن منو در UI کافی نیست (دروازه Middleware قبل از Routing).
- [x] Active/Restricted/Unlicensed با تست HTTP واقعی روی Pipeline کامل پوشش داده شده‌اند.
- [ ] تست E2E با License واقعی امضاشده (import → Active → تغییر اعتبار → Restricted → تمدید) در راهنمای Windows Localhost و E2E بعدی؛ در این PR مسیر import با تست‌های موجود و unit/HTTP tests پوشش داده شده است.

## خلاصه تغییرات
- `ILicenseRuntimeStateService` + `LicenseRuntimeStateService`: هر بار از `ICustomerLicenseRuntimeEvaluator` (revocation-aware، بدون cache) وضعیت را می‌سازد؛ نبود هویت نصب/Envelope → Unlicensed؛ هر رد اعتبارسنجی → Invalid؛ payload فقط روی نتیجهٔ validated.
- `LicenseRuntimeGateMiddleware`: دروازهٔ سراسری قبل از Routing؛ allow-list فعال‌سازی/بازیابی؛ قواعد Dedicated/Cloud برای Restricted؛ پاسخ JSON با کد ثابت (`license_required`، `license_invalid`، `license_restricted`، `license_cloud_expired`) و `no-store`؛ لاگ فقط metadata.
- `LicenseRuntimeGateOptions` (`Licensing:RuntimeGate:Enabled`، پیش‌فرض **خاموش**) + `Licensing:DeploymentMode` (`Dedicated`/`Cloud`؛ نامعتبر → fail-fast در startup با ValidateOnStart).
- endpoint `GET /api/v1/licensing/status` (AllowAnonymous + ApiSensitivePolicy) برای Activation Wizard و شِل React.
- هیچ Migration/Schema جدیدی وجود ندارد؛ همهٔ داده‌های لازم (InstalledLicenses/InstallationIdentities) از Migrationهای قبلی است.

## فایل‌های مهم تغییرکرده
- `Server/Infrastructure/Licensing/LicenseRuntimeGateOptions.cs` — گزینه‌ها، مود استقرار، enum وضعیت
- `Server/Infrastructure/Licensing/LicenseRuntimeStateService.cs` — snapshot و service
- `Server/Infrastructure/Licensing/LicenseRuntimeGateMiddleware.cs` — دروازه + DI extension
- `Server/Api/ApiEndpoints.cs` — endpoint وضعیت
- `Server/Program.cs` — ثبت services و middleware (بعد از setup، قبل از Routing)
- `tests/Template.Tests/Licensing/LicenseRuntimeGateTests.cs` — ۲۳ تست واحد (state mapping + رفتار دروازه)
- `tests/Template.Tests/Licensing/LicenseRuntimeGateHttpTests.cs` — ۸ تست HTTP روی Pipeline واقعی (gate روشن/خاموش)

## تصمیم‌های فنی
- **پیش‌فرض خاموش (opt-in):** با `Licensing:RuntimeGate:Enabled=false` رفتار میزبان عین قبل است تا CI/E2E موجود و استقرارهای فعلی بی‌تغییر بمانند؛ روشن‌کردن آن تصمیم عملیاتی استقرار است (هم‌الگوی Gate صدور v5). این انتخاب «enforcement واقعی» را حذف نمی‌کند — با روشن بودن، enforcement کامل سمت سرور اعمال و با تست HTTP اثبات می‌شود.
- **بدون cache:** هر درخواست وضعیت را دوباره resolve می‌کند (D-058: هیچ ردیف DB به‌تنهایی اثبات نیست). هزینهٔ ECDSA/DB بهینه‌سازی بعدی می‌خواهد و در D-058 §Cloud به cache نسخه‌دار server-controlled موکول است.
- **Restricted:** سیاست ساده و محافظه‌کارانهٔ «فقط خواندن + allow-list + logout» به‌جای طبقه‌بندی endpointها؛ هیچ grace ضمنی ندارد.
- تصمیم معماری جدیدی گرفته نشد؛ اجرای D-060/D-098 است.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet build Server/Server.csproj` | موفق — ۰ خطا | Linux، SDK 10.0.100 |
| `dotnet build tests/Template.Tests` | موفق — ۰ خطا | |
| `dotnet test --filter "FullyQualifiedName~LicenseRuntime"` | **۳۱ موفق / ۰ شکست** | unit + HTTP روی Pipeline واقعی (InMemory fallback) |
| `dotnet test Template.Tests` کامل | نتیجه در PROJECT_STATUS ثبت می‌شود | SQL Server-gatedها در CI اجرا می‌شوند |
| تست با License واقعی import‌شده | اجرا نشده | راهنمای Windows Localhost + E2E بعدی |

## امنیت، داده و سازگاری
- هیچ Migration و تغییر داده‌ای؛ فعال‌سازی gate هیچ Schema جدیدی نمی‌خواهد.
- پاسخ‌های خطا کد ثابت و بدون payload/entitlement دارند؛ لاگ metadata-only است.
- endpoint وضعیت Anonymous است اما فقط projection payload امضاشده را می‌دهد (بدون PII؛ CompanyReference مات است) و rate-limited است.
- ترتیب کنترل D-074 دست‌نخورده: دروازه قبل از auth/permission عمل می‌کند و جایگزین عضویت/مجوز عملیاتی نیست.

## ریسک‌ها و کارهای باقی‌مانده
- رزولوشن هر درخواست = یک خواندن DB + یک verify امضا؛ در بار بالا cache نسخه‌دار طبق D-058 لازم می‌شود (PR بعدی پس از اندازه‌گیری).
- صفحهٔ Activation Wizard در React هنوز وجود ندارد؛ تا آن زمان مسیر فعال‌سازی API/فایل است (همان مسیر import/apply موجود).
- سقف نشست هم‌زمان (D-100) و Company/Module entitlement enforcement در APIهای عملیاتی (#391/#401) بعد از این دروازه ادامه می‌یابد.

## شناسه‌های Git
- Branch: `agent/396-license-runtime-gate`
- Commit: `7f81a5f3`
- Pull Request: #721
