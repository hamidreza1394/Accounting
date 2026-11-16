# Runbook عملیاتی — Provisioning شرکت‌محور v5 و نگاشت CompanyReference (Issue #392)

- **تاریخ:** 2026-11-16 — **مبنا:** `main@b05c1c58` + PRهای بازسازی #684، #686، #687، (این PR)
- **قراردادهای مرجع:** D-059 (§2، §6، §14)، D-060، طرح مصوب مالک 2026-11-16، `docs/LICENSING.md`، `docs/SECURITY.md`
- **وضعیت صدور عملیاتی v5:** **خاموش** — Gate دوکلیدی Vendor (`Provisioning:CompanyReferenceIssuance:Enabled` + `CustomerConsumerReady=false`) تغییری نکرده است. هیچ بخش از این Runbook خودِ صدور را روشن نمی‌کند.

## ۱) جریان کاری از دید اپراتور مشتری

1. Vendor بستهٔ Provisioning v5 را صادر می‌کند (پس از روشن‌شدن Gate فقط با دستور مالک).
2. مشتری بسته را به `ApplyAsync` می‌دهد (مسیر عملیاتی موجود #235). Verifier امضا، InstallationId، انقضا و Revocation را fail-closed بررسی می‌کند.
3. برای بستهٔ شرکت‌محور، ترتیب بررسی‌ها ثابت است: **Verifier → Claim/Replay → بررسی‌های v1–v4 (والد، مدیر اولیه) → Entitlement → Duplicate → Capacity** و سپس Company + `CompanySetupRecord` در یک `SaveChanges` اتمیک داخل تراکنش `Serializable` + `sp_getapplock`.
4. Bootstrap مدیر اولیه از زیرساخت موجود v1–v4 استفاده می‌کند (توکن یک‌بارمصرف Enrollment؛ بدون رمز پیش‌فرض؛ D-059 §12-13).

## ۲) Migration

- Migration جدید: `20261116070805_AddCompanySetupRecords` — **فقط یک جدول جدید افزایشی**؛ هیچ جدول دیگری تغییر نمی‌کند؛ `Down` دقیقاً معکوس است.
- اعمال فقط با مسیر کنترل‌شدهٔ مستند (`dotnet run --project Server -- --migrate` پس از بازبینی Schema) یا در چرخهٔ استقرار؛ **هرگز بدون بازبینی و بدون مجوز روی دیتابیس مشتری اجرا نشود** (قاعدهٔ DATABASE.md و D-020/D-025).
- دیتابیس‌های قدیمی `EnsureCreated()` جدول را ندارند تا در مسیر کنترل‌شده مهاجرت شوند؛ تا آن زمان بستهٔ شرکت‌محور fail-closed رد می‌شود (`provisioning_apply_conflict` روی نبود جدول) و بسته‌های v1–v4 بدون تغییر کار می‌کنند.

## ۳) ماتریس خطای قطعی بستهٔ شرکت‌محور

| وضعیت درخواست | کد خطا | رفتار Replay |
|---|---|---|
| تکرار دقیق همان RequestId/Nonce | همان ACK ذخیره‌شده (Applied یا Rejected) | بازپخش بایت‌به‌بایت |
| `LicenseId` نصب‌نشده/نامنطبق با این Installation | `provisioning_license_not_installed` | ACK رد ذخیره و بازپخش |
| مرجع ∈ entitlement لایسنس نیست | `provisioning_not_entitled_for_company` | همان |
| مرجع مجاز ولی قبلاً نگاشت‌شده | `provisioning_company_reference_conflict` | همان؛ مستقل از ظرفیت |
| مرجع جدید و ظرفیت پر | `provisioning_company_capacity_exceeded` | همان |

- ACKهای رد سمت Vendor فقط وقتی وضعیت درخواست را به Failed می‌برند که allow-list Vendor با این چهار کد گسترش یابد (PR مستقل Vendor، نیازمند تأیید مالک). تا آن زمان رفتار fail-closed موجود حفظ می‌شود.
- شمارنده‌های ظرفیت (تعداد فعلی، سقف) در لاگ برنامه ثبت می‌شوند و در ACK قرار نمی‌گیرند (شمای ACK نسخه‌دار و ثابت است).

## ۴) معنای `maxCompanies` (تصمیم مصوب مالک 2026-11-16)

- ملاک، **تعداد کل شرکت‌های نصب** است (فعال/غیرفعال، Legacy/v5) — سقف در سطح Installation است (D-059 §2).
- عبور از سقف فقط «ایجاد جدید» را مسدود می‌کند؛ هیچ شرکتی حذف یا غیرفعال نمی‌شود و عملیات موجود ادامه دارد (D-059 §6، block-new).
- نبود `MaxCompanies` در پیلود امضاشده یعنی سقف عددی ندارند؛ فهرست entitlement همچنان مرز مجاز است.
- **راهنمای صدور برای Vendor:** هنگام صدور/amendment لایسنس، `MaxCompanies ≥ تعداد شرکت‌های فعلی مشتری` انتخاب شود تا ناسازگاری عملیاتی ایجاد نشود؛ در صورت رد با `provisioning_company_capacity_exceeded`، اصلاح از مسیر لایسنس/amendment جدید با سقف درست انجام می‌شود (جزئیات شمارش در لاگ مشتری).

## ۵) عملیات تکرار، خطا و بازیابی

- **ارسال دوبارهٔ همان بسته:** همیشه همان ACK قبلی؛ هیچ اثر جانبی جدیدی تولید نمی‌شود.
- **ارسال دوباره با بستهٔ جدید برای همان مرجع:** `provisioning_company_reference_conflict` — عملیات درست، «شرکت از قبل موجود» است.
- **شکست وسط درج:** کل تراکنش Rollback می‌شود؛ نه Company و نه Record باقی نمی‌ماند (تست SQL Server-بستهٔ `FailingSave_RollsBackCompanyAndMappingAtomically`).
- **Restore دیتابیس به عقب:** ردیف‌های Claim/ACK/نگاشت هم به عقب برمی‌گردند؛ این سناریو بخشی از تصمیم باز OD-06/#364 است و تا تصمیم مالک، پوشش اضافه‌ای غیر از رفتار fail-closed موجود وعده داده نمی‌شود. همین محدودیت برای Clone کامل VM/میزبان (T6/T32) برقرار است.

## ۶) چک‌لیست فعال‌سازی صدور v5 (فقط مالک، پس از Merge هر ۴ PR)

1. Merge ترتیبی #684 → #686 → #687 → این PR.
2. PR مستقل Vendor: گسترش allow-list کدهای رد ACK (`provisioning_license_not_installed`، `provisioning_not_entitled_for_company`، `provisioning_company_reference_conflict`، `provisioning_company_capacity_exceeded`).
3. تغییر هم‌زمان `VendorProvisioningCompanyReferenceGate.CustomerConsumerReady=true` و تست پین آن (تست جهش سبز).
4. تنظیم `Provisioning:CompanyReferenceIssuance:Enabled=true` فقط در استقرار Vendor مورد نظر.
5. اجرای تست پین Vendor + Dual-Host E2E واقعی دو میزبان پیش از صدور عملیاتی.
6. هیچ Migration مشتری به‌تنهایی اجرا نمی‌شود؛ جدول در چرخهٔ استقرار استاندارد مشتری اعمال می‌شود.

## ۷) شواهد آزمون (خلاصه)

- تست‌های SQL Server واقعی (در CI با SQL Server 2022): schema/BIN2/Restrict/Up-Down؛ خوش‌مسیر+Replay؛ عدم-مجوز؛ تکرار؛ ظرفیت با Legacy و block-new؛ نبود سقف؛ Superseded؛ Rollback اتمیک؛ Race هم‌زمانی.
- محلی بدون SQL: همهٔ InMemory سبز؛ دو شکست Baseline شناخته‌شدهٔ `SafeExceptionRealApplicationTests` (در CI با SQL پاس می‌شوند) دست‌نخورده مانده‌اند.
- هیچ تستی برای سبزشدن حذف یا غیرفعال نشده است.
