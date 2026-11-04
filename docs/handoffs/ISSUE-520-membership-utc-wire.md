# تحویل وظیفه — ISSUE-520-membership-utc-wire

- **Issue:** #520
- **Agent/مسئول:** Arena
- **تاریخ:** 2026-11-04
- **وضعیت:** پیاده‌سازی‌شده؛ تأیید Build و تست‌ها مسدود

## هدف و معیارهای پذیرش
- [x] برچسب UTC پس از materialization برای سه پاسخ عضویت، بدون تغییر tick.
- [x] تست واحد Kind و تست SQL Server برای JSON و فیلدهای قدیمی نوشته شد.
- [ ] اجرای Build و تست‌ها: SDK و SQL Server در محیط موجود نیستند.

## خلاصه تغییرات و فایل‌ها
- `Server/Api/UserAdministrationEndpoints.cs`، `CompanyUserDirectoryEndpoints.cs` و `CompanyRoleAdministrationEndpoints.cs`: برچسب‌گذاری صرفاً زمان عضویت بعد از اجرای query.
- `tests/Template.Tests/Integration/MembershipUtcWireFormatTests.cs`: تست سه مسیر با provider حافظه و SQL Server؛ بررسی tick و قرارداد فیلدهای قدیمی.
- `docs/TIME_CONTRACT.md`: یادداشت اصلاح محدود projection؛ `docs/WORK_LOG.md`: ثبت تحویل.

## تصمیم‌های فنی
- `DateTime.SpecifyKind` صرفاً معنای UTC مقادیر ذخیره‌شده UTC را بازمی‌گرداند، بدون تبدیل منطقه زمانی. مبدل نام‌محور EF از پیش در کد وجود داشته؛ مبدل دیگری اضافه نشده است.
- تست حافظه مبدل عضویت را در مدل تست حذف می‌کند تا ورودی Unspecified و اصلاح پس از خواندن را مستقل آزمایش کند.

## بررسی و آزمون
| فرمان/آزمون | نتیجه واقعی | توضیح |
|---|---|---|
| `dotnet restore Template.sln` | اجرا نشد | dotnet پیدا نشد (127) |
| `dotnet build Template.sln --no-restore --configuration Release` | اجرا نشد | dotnet پیدا نشد (127) |
| `dotnet test tests/Template.Tests/Template.Tests.csproj --configuration Release` | اجرا نشد | dotnet پیدا نشد (127) |
| `scripts/dev/install-dotnet.sh --check` | ناموفق | دسترسی به builds.dotnet.microsoft.com برقرار نشد |
| `python3 scripts/check_static_assets.py` | موفق | بدون ارجاع CDN |
| `git diff --check` | موفق | بررسی فاصله‌های نامعتبر |

## امنیت، داده و سازگاری
احراز هویت، Tenant، نام و نوع فیلدها، فیلدهای محلی قدیمی و schema تغییر نکردند؛ Migration اضافه نشد.

## ریسک‌ها و کارهای باقی‌مانده
Build و تست واحد و SQL Server باید در CI یا محیط مجهز به SDK 10 و SQL Server واقعاً اجرا شوند. تا آن زمان صحت اجرای تست‌ها تأیید نشده است.

## شناسه‌های Git
- Branch: `arena/55f4156d-accounting`
- Commit / PR: در تاریخچه Git و صفحه PR قابل مشاهده است.
