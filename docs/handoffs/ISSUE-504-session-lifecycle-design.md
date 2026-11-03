# تحویل طراحی — ISSUE-504-session-lifecycle-design

- Issue: #504
- Parent: #444 / S7
- ADR: D-069 (Proposed)
- Branch: agent/504-session-lifecycle-design-mainline
- Status: design only; no runtime/schema changes.

## ممیزی
- Cookie ASP.NET Core با idle timeout برابر ۳۰ دقیقه و SlidingExpiration فعال است.
- SessionValidator در هر درخواست account active و CredentialStamp را بررسی می‌کند؛ تغییر رمز همه Cookieهای قدیمی را بی‌اعتبار می‌کند.
- خروج صریح API با UserSessionAuditService رویداد LogoutSucceeded را ثبت می‌کند؛ پس از آن Cookie و WorkContext حذف می‌شوند.
- SessionId و جدول UserSession وجود ندارد؛ بنابراین ابطال مستقل یا فهرست نشست‌های فعال امکان‌پذیر نیست.
- از نبود Logout event نمی‌توان expiry، logout یا مدت نشست را استنتاج کرد.

## پیشنهاد
- یک UserSession برای هر ورود موفق کاربر؛ SessionId در Claim کوکی محافظت‌شده.
- اعتبارسنجی در هر درخواست؛ session missing/revoked/wrong-user => deny.
- حفظ ۳۰ دقیقه sliding فعلی؛ active = not revoked + LastSeenAtUtc در ۳۰ دقیقه اخیر؛ به‌روزرسانی LastSeen حداکثر هر ۵ دقیقه.
- self-service list/revoke/revoke-others؛ revoke نشست جاری باعث sign-out فوری.
- تغییر رمز همه نشست‌ها را revoke می‌کند؛ CredentialStamp حفظ می‌شود.
- عدم ذخیره IP خام و User-Agent در UserSession؛ حساب Infrastructure خارج از این API.
- retention و مجوز مدیر نصب برای revoke نشست دیگران نیازمند تأیید مستقل است؛ تا آن زمان هیچ cleanup job یا migration انجام نمی‌شود.

## مالک باید تأیید کند
1. ۳۰ دقیقه idle و نبود absolute lifetime.
2. revoke نشست جاری و revoke-others.
3. retention نشست‌های revoked/idle و audit.
4. عدم ذخیره IP/User-Agent.
5. scope مدیر نصب برای ابطال نشست دیگر کاربران.

## عدم تغییر
این PR فقط ADR و مستندات است. هیچ Migration، claim، endpoint یا تغییر رفتار Cookie در آن وجود ندارد.
