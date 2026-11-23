# handoff به مسئول Trade (Claude) — اتصال مجوز `TRADE_IMPORT` به لایسنس امضاشده (Issue #401)

- **تهیه:** GLM (zgo/glm-5.3-flash) — 2026-11-23 — فقط مستندات؛ هیچ کد Trade/`DenyAllTradeModuleEntitlement` تغییر نکرده است.
- **اصل غیرقابل‌مذاکره (D-025/D-074):** وجود entitlement تجاری به‌تنهایی **مجوز عملیاتی کاربر نیست**؛ دو کنترل مستقل‌اند. هیچ استثنایی برای `TRADE_IMPORT` در validatorها اضافه نشده و نباید اضافه شود.

## 1) قرارداد دقیق entitlement مورد انتظار (payload v2)

- پاکت امضاشدهٔ Vendor (بعد از PR #781 و Key Ceremony Staging) حاوی `LicensePayload` v2 است؛ برای هر شرکت یک `CompanyLicenseEntitlement`:
  - `CompanyReference`: opaque `Guid` سبک "D" تولیدشدهٔ سمت سرور Vendor — **نه** CompanyId، **نه** TenantId، **نه** CustomerBoundaryId (D-038).
  - `Modules`: `["TRADE_IMPORT"]` — تنها کد Trade مجاز در `LicenseSubsystemCodes.IsAllowedCompanyModule` (فهرستallow شامل `finance|sales|inventory|treasury|TRADE_IMPORT`).
  - `MaxUsers`: ظرفیت نشست‌های همان شرکت؛ `Features`: فهرست (در چرخهٔ اول خالی).
- کل پاکت فقط از `VendorSignedLicenseEnvelopes` → endpoint صادرات `GET /api/vendor/licenses/{id}/envelope/export` → فایل → import مشتری می‌آید. منبع حقیقت، payload امضاشده است؛ هیچ دادهٔ UI/کلاینت مبنای entitlement نیست.

## 2) محل resolve شدن CompanyReference (Customer-side)

- روی اعمال (Apply) در `CompanySetupRecords` (#684/#686؛ Migration `20261116070805` — **فقط در مخزن، روی DB واقعی اجرا نشده**).
- قرارداد v5 provisioning (`allowCompanyReference`) پشت Gate دوکلیدی بسته است (`CustomerConsumerReady=false`)؛ **برای چرخهٔ اول provisioning v5 باز نمی‌شود** — binding شرکت‌محور از مسیر import لایسنس + mapping شرکت جدا انجام می‌شود و این حلقه باید در مأموریت Customer (#392) بسته شود.
- تا mapping معتبر، هر خوانش entitlement شرکت‌محور باید fail-closed باشد؛ نباید از CompanyId یا دادهٔ محلی به CompanyReference حدس زده شود.

## 3) وضعیت واقعی زنجیره (تاریخ 2026-11-23)

| حلقه | وضعیت |
|---|---|
| ساخت/تأیید/صدور لایسنس شرکت‌محور v2 با `TRADE_IMPORT` در Vendor | Implemented (فرم/UI + payload builder + issuance تراکنشی؛ امضا fail-closed تا کلید CNG Staging طبق D-107 آماده شود) |
| صادرات پاکت از Vendor | PR #781 باز (در انتظار بازبینی/CI) |
| اعتبارسنجی و persist پایدار در Customer | کد در main؛ **Operationally NotRun** (بدون Migration واقعی و بدون اجرای واقعی) |
| Runtime entitlement enforcement شرکت‌محور در call-siteهای Trade | **NotImplemented** (#391؛ `DenyAllTradeModuleEntitlement` فعلی دست‌نخورده می‌ماند) |
| ACK/Applied tracking لایسنس | بسته به تصمیم قرارداد S-B مالک (#393/#394) |

## 4) API/abstraction مورد نیاز سمت Trade (پیشنهاد، نه توافق قراردادی)

- Trade فقط از لایهٔ تصمیم موجود (`EntitlementPolicyEngine` → bridge payload معتبر در #539) و سرویس runtime لایسنس مشتری مصرف کند: ورودی `CompanyReference`+ماژول، خروجی اجازهٔ ماژول؛ **هیچ خوانش مستقیم جدول لایسنس یا JSON خام در Trade.Application/Infrastructure ممنوع.**
- ترتیب کنترل هر عملیات Trade (S-D §۲.۱): نشست ← عضویت ← WorkContext/شرکت فعال ← entitlement ماژول (از payload امضاشده) ← مجوز عملیاتی کاربر (`trade.*`).
- هیچ fallback امنیتی، grace یا استثنای محیطی برای تست اضافه نشود.

## 5) تست‌های لازم (شرط شروع تست واقعی Trade)

1. **Company A** با entitlement `TRADE_IMPORT`: عملیات ماژول با مجوز کاربر باز می‌شود؛ ثبت Audit.
2. **Company B** بدون آن entitlement در همان Installation: همان کاربر/کاربر دیگر با مجوز `trade.*` هم باز می‌شود؟ نه — Gate entitlement مستقل از RBAC است و برای B fail-closed می‌ماند؛ هر دو محور مستقل تست شوند (مثبت A، منفی B، منفی B با مجوز کامل کاربر).
3. پاکت بدون `TRADE_IMPORT` / امضای نامعتبر / منقضی → ماژول بسته می‌ماند؛ هیچ مسیر واگذاری دیتای Company به شرکت دیگر باز نشود.
4. Restart و قطع شبکه رفتار وضعیت پایدار را تغییر ندهد.

## 6) مراجع

`docs/runbooks/FIRST-STAGING-LICENSE-CYCLE.md`؛ `docs/handoffs/ISSUE-UNNUMBERED-vendor-to-customer-first-license-cycle-handoff.md`؛ `docs/handoffs/ISSUE-391-runtime-entitlement-enforcement.md`؛ `docs/ENTITLEMENT_ENFORCEMENT_CALLSITE_INVENTORY.md`؛ `docs/TRADE_MODULE.md` (D-061/D-062)؛ D-059/D-060/D-109/D-117 در `docs/DECISIONS.md`.
