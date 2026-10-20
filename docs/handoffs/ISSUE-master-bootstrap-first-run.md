# تحویل — Master bootstrap first-run

## شاخه
`feature/master-bootstrap-first-run`

## خلاصه
- سرویس `InfrastructureMasterBootstrap`: اعتبارسنجی (رمز ≥ ۱۵)، هش با `PasswordHashService`، تولید دستورات User Secrets و Env.
- بدون Seed جدول Users.
- بدون ذخیره Secret جدید روی دیسک برنامه (محدودیت معماری تأییدشده).
- UI در `/DatabaseSetup` پس از موفقیت DB و دروازه TempData.
- Restart پس از تنظیم Configuration لازم است.
- تست‌ها: `InfrastructureMasterBootstrapTests`.

## فایل‌ها
- Server/Infrastructure/DatabaseSetup/InfrastructureMasterBootstrap.cs
- Server/Pages/DatabaseSetup.cshtml / .cs
- Server/Program.cs
- tests/Template.Tests/InfrastructureMasterBootstrapTests.cs
- docs/SECURITY.md, docs/DEVELOPMENT.md (بخش جدید)

## Build/Test
در محیط Agent SDK موجود نیست؛ CI باید `dotnet test` را اجرا کند.
