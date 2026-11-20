# ممیزی نهایی چرخه Vendor — مرحله پنجم مأموریت (2026-11-20)

این سند وضعیت واقعی Issueها، امنیت، هزینه و آمادگی Production را با کد واقعی Main + شاخه‌های زنجیره‌ای #739/#740/#741 تطبیق می‌دهد. هیچ Issueای صرفاً به‌دلیل وجود کد Completed اعلام نشده است.

## ۱. وضعیت Issueهای مرتبط (بررسی‌شده از GitHub + کد)

| Issue | وضعیت GitHub | وضعیت ممیزی | شواهد کد | اقدام لازم |
|---|---|---|---|---|
| #107 — یکتایی زنجیره Audit و checkpointهای مستقل | open | **Partially Implemented** | `VendorAuditWriter` درون-تراکنشی با hash chain (`VendorAuditEvent`، PreviousHash/EventHash) و شکست Audit → Rollback؛ checkpoint مستقل/آرشیو دوره‌ای هنوز پیاده نشده | پیاده‌سازی checkpoint + سیاست Retention/Archive مستند |
| #214 — صدور/ابطال fail-closed با Maker-Checker | open | **Implemented / Verified** | approval workflow + `VendorLicenseIssuanceService` داخل تراکنش + unique sequence؛ مدل تک‌عملیات مجاز طبق D-109 | بستن با تأیید مالک |
| #228 — Onboarding امن Vendor | open | **Implemented / Verified** | package create (RequestId+Nonce unique، dedupe) → approval دو مجوزه → امضا → export → ACK | بستن با تأیید مالک |
| #235 — مصرف‌کننده امضاشده سمت مشتری + ACK | open | **Implemented / Verified** | `CustomerProvisioningApplyService` + ACK امضاشده با trust store سمت وند | بستن با تأیید مالک |
| #236 — Signer عملیاتی با KMS/HSM خارجی | open | **Blocked (نیازمند زیرساخت)** | `UnavailableLicenseSigningProvider` fail-closed؛ Windows CNG runbook (#736) آماده؛ HSM/KMS واقعی تأمین نشده | تأمین زیرساخت + key ceremony (D-107) |
| #389 — Entitlement شرکت‌محور و add-onها | open | **Blocked** | وابسته به #392؛ builder شرکت‌محور در جریان Amendment عمداً expose نشده | پس از #392 |
| #390 — نسخه‌بندی Entitlement شرکت‌محور | closed | **Implemented / Verified** | payload v2 + `CompanyEntitlementsDraftJson` + تست‌ها | بسته‌شده صحیح |
| #391 — سقف seats/modules سمت مشتری | closed | **Implemented / Verified** | resolver v2/v5 سمت مشتری + تست‌ها | بسته‌شده صحیح |
| #392 — ایجاد شرکت‌های مجاز و Bootstrap آفلاین | open | **Partially Implemented / Needs Owner Decision** | `CompanySetupRecord` + v5 + gate بسته (default-off)؛ mapping عملیاتی نشده | تصمیم مالک + فعال‌سازی gate پس از ممیزی |
| #393 — فروش و صدور add-onها | open | **Partially Implemented** | سفارش/پرداخت/مطالبات (#722+#738) + Amendment نصب‌محور (#739) کامل؛ شرکت‌محور Blocked؛ ACK tracking اختصاصی Amendment در مسیر provisioning موجود | ادامه پس از #392 |
| #394 — همگام‌سازی آنلاین + تبادل آفلاین امضاشده | open | **Partially Implemented / Needs Verification** | offline revocation snapshots + activation wizard؛ سناریوهای همگام‌سازی دوره‌ای کامل ممیزی نشده | ممیزی مستقل + تصمیم مالک |
| #396 — گیت startup و فعال‌سازی آنلاین/آفلاین | open | **Implemented / Verified** | Runtime Gate سمت مشتری + wizard RTL (#735) | بستن با تأیید مالک |

## ۲. ممیزی امنیت (خلاصه شواهد)

- **Authentication/Authorization:** مجوزهای نسخه‌بندی‌شده سمت سرور؛ resource-bound policy برای تصمیم‌ها؛ MFA اجباری ورود مدیر (TOTP/Passkey).
- **Isolation:** همه queryهای resource-bound با `VendorResourceAccessEvaluator` روی مالکیت واقعی؛ مسیر ACK با customerId+installationCode در envelope امضاشده مقایسه می‌شود.
- **Bootstrap/Credential:** رمز اولیه تصادفی غیرقابل استفاده؛ Enrollment یک‌بارمصرف (hash+انقضا+مصرف اتمی+rate-limit)؛ هیچ Secret در Log/Audit/URL.
- **Signing/Trust:** D-056 fail-closed؛ trust store deployment-based؛ verify امضا+payload-hash پیش از هر rebuild کاندید Amendment؛ sequence یکتا (LicenseId,Sequence).
- **Concurrency/Idempotency:** RowVersion همه mutationها؛ uniqueهای فیلترشده؛ retry دقیق ACK؛ دو SaveChanges با پین RV در approval Amendment.
- **Audit atomicity:** AuditWriter در تراکنش caller؛ شکست → Rollback کامل (تست SQL دارد).
- **Antiforgery:** همه Mutationهای وند؛ صفحات فقط-خواندنی GET.
- **نقطه‌های ریسک باقی‌مانده:** هیچ کلید عملیاتی Production (نرم‌افزاری ممنوع؛ #236)، checkpoint مستقل Audit (#107)، سناریوهای همگام‌سازی #394.

## ۳. ممیزی هزینه و پیچیدگی

- بدون جدول/سرویس موازی برای قابلیت‌های موجود؛ Amendment فقط یک جدول جدید با داده غیرمشتق اضافه کرد.
- Readiness Diagnostics فقط-خواندنی با چند query ایندکس‌شده در زمان خواندن — بدون Background Worker، Message Broker یا Cache جدید؛ برای ~۱۰۰ کاربر هم‌زمان کافی است.
- Audit/Log: نیاز به سیاست Retention/Archive مصوب برای رشد بلندمدت (پیشنهاد: ADR مستقل — تصمیم مالک).
- هیچ Database یا سرویس ابری جدیدی ایجاد نشد.

## ۴. آمادگی Production

**Not Ready — Blocked on:**
1. تأمین HSM/KSM واقعی و key ceremony (#236/D-107) — تا آن زمان صدور fail-closed است.
2. حل #392 و فعال‌سازی gate شرکت‌محور با تأیید مالک.
3. checkpoint/retention مستقل Audit (#107).
4. ممیزی سناریوهای همگام‌سازی #394.
هیچ‌یک از این‌ها با فرض یا دور زدن حل نشده‌اند.

## ۵. وضعیت نهایی مأموریت

- **Implementation:** Completed برای مراحل ۲–۴ (زنجیره #739→#740→#741)؛ Blocked/Needs-Owner برای موارد جدول بالا.
- **Tests:** Verified (Unit + SQL CI + Vitest + Semgrep + CI سبز روی هر head).
- **PRs:** #739 باز آماده Merge؛ #740 و #741 Draft زنجیره‌ای.
- **Merge:** Awaiting Owner — هیچ PRای Merge نشده و هیچ Push مستقیم به Main انجام نشده است.
