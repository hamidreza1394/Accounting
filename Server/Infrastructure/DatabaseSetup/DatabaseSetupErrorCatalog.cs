using System.Security.Authentication;
using Microsoft.Data.SqlClient;

namespace Infrastructure.DatabaseSetup;

/// <summary>Pure classification of setup failures and their Persian message and remedy.</summary>
public static class DatabaseSetupErrorCatalog
{
    public static SqlException? FindSqlException(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                return sql;
            }
        }

        return null;
    }

    public static DatabaseSetupErrorCode Classify(DatabaseSetupStage stage, int? sqlNumber, Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is DatabaseSettingsStoreException store)
            {
                return store.Code;
            }

            if (current is AuthenticationException ||
                (current is SqlException && current.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase)))
            {
                return DatabaseSetupErrorCode.CertificateNotTrusted;
            }
        }

        var fromNumber = sqlNumber switch
        {
            18456 => DatabaseSetupErrorCode.LoginFailed,
            18452 => DatabaseSetupErrorCode.WindowsAuthNotTrusted,
            18470 or 18487 or 18488 => DatabaseSetupErrorCode.LoginDisabledOrExpired,
            53 or 2 or 10060 or 11001 or -1 => DatabaseSetupErrorCode.ServerUnreachable,
            26 or 40 => DatabaseSetupErrorCode.InstanceNotFound,
            -2 or 258 => DatabaseSetupErrorCode.Timeout,
            233 or 10054 => DatabaseSetupErrorCode.ConnectionClosed,
            4060 or 916 => DatabaseSetupErrorCode.DatabaseNoAccess,
            262 or 229 or 15247 => DatabaseSetupErrorCode.PermissionDenied,
            5170 or 5133 or 5120 or 1802 => DatabaseSetupErrorCode.DatabaseFileError,
            1105 or 9002 => DatabaseSetupErrorCode.DiskFull,
            _ => DatabaseSetupErrorCode.None,
        };

        if (fromNumber != DatabaseSetupErrorCode.None)
        {
            return fromNumber;
        }

        // The managed SNI used on Linux reports a refused or unresolvable TCP connection as number 0,
        // severity 20 ("A network-related or instance-specific error ..."): the server was not reached.
        if (sqlNumber == 0 && FindSqlException(exception) is { Class: >= 20 })
        {
            return DatabaseSetupErrorCode.ServerUnreachable;
        }

        if (exception is TimeoutException)
        {
            return DatabaseSetupErrorCode.Timeout;
        }

        if (stage == DatabaseSetupStage.SavingSettings)
        {
            if (exception is UnauthorizedAccessException)
            {
                return DatabaseSetupErrorCode.SettingsAccessDenied;
            }

            if (exception is IOException)
            {
                return DatabaseSetupErrorCode.SettingsIoError;
            }

            if (exception is System.Security.Cryptography.CryptographicException)
            {
                return DatabaseSetupErrorCode.SettingsProtectionFailed;
            }
        }

        return stage == DatabaseSetupStage.RunningMigrations
            ? DatabaseSetupErrorCode.MigrationFailed
            : DatabaseSetupErrorCode.Unknown;
    }

    public static string DescribeWarning(string code) => code switch
    {
        DatabaseSettingsWarnings.WindowsAclNotApplied =>
            "محدودسازی دسترسی پوشهٔ تنظیمات (فقط کاربر جاری، SYSTEM و Administrators) اعمال نشد؛ دسترسی پوشه را دستی بررسی کنید.",
        DatabaseSettingsWarnings.SetupTokenNotDeleted =>
            "تنظیمات ذخیره شد اما فایل کد راه‌اندازی حذف نشد؛ کد دیگر معتبر نیست، فایل را دستی پاک کنید.",
        _ => "هشدار: " + code,
    };

    public static (string Message, string Remedy) Describe(DatabaseSetupErrorCode code) => code switch
    {
        DatabaseSetupErrorCode.ValidationFailed => (
            "اطلاعات فرم معتبر نیست.",
            "پیام کنار هر فیلد را اصلاح و دوباره تلاش کنید."),
        DatabaseSetupErrorCode.LoginFailed => (
            "ورود به SQL Server ناموفق بود؛ نام کاربری یا رمز عبور درست نیست.",
            "نام کاربری و رمز را بررسی کنید و مطمئن شوید ورود SQL Server (Mixed Mode) فعال است."),
        DatabaseSetupErrorCode.WindowsAuthNotTrusted => (
            "حساب ویندوز فعلی به SQL Server اعتماد ندارد (Windows Authentication پذیرفته نشد).",
            "برنامه را با حسابی اجرا کنید که در SQL Server Login دارد، یا روش «نام کاربری و رمز SQL Server» را انتخاب کنید."),
        DatabaseSetupErrorCode.LoginDisabledOrExpired => (
            "Login این حساب در SQL Server غیرفعال، قفل یا منقضی شده است.",
            "مدیر SQL Server باید Login را فعال یا رمز آن را تمدید کند."),
        DatabaseSetupErrorCode.CertificateNotTrusted => (
            "گواهی TLS سرور SQL Server مورد اعتماد نیست.",
            "گواهی معتبر روی سرور نصب کنید؛ فقط در محیط مورد اعتماد گزینه «اعتماد به گواهی سرور» را فعال کنید."),
        DatabaseSetupErrorCode.ServerUnreachable => (
            "به سرور SQL Server دسترسی پیدا نشد.",
            "نام یا نشانی سرور، پورت، Firewall و روشن‌بودن سرویس SQL Server را بررسی کنید."),
        DatabaseSetupErrorCode.InstanceNotFound => (
            "سرور یا نمونه (Instance) SQL Server پیدا نشد.",
            "نام Instance را بررسی کنید (مثلاً .\\SQLEXPRESS) و سرویس SQL Server Browser یا پورت را کنترل کنید."),
        DatabaseSetupErrorCode.Timeout => (
            "مهلت اتصال یا اجرای دستور به پایان رسید.",
            "شبکه و بار سرور را بررسی کنید و دوباره تلاش کنید."),
        DatabaseSetupErrorCode.ConnectionClosed => (
            "اتصال توسط سرور یا شبکه قطع شد.",
            "نسخه TLS، Firewall و تنظیمات Encrypt سرور را بررسی کنید و دوباره تلاش کنید."),
        DatabaseSetupErrorCode.DatabaseNoAccess => (
            "این حساب اجازه دسترسی به دیتابیس انتخابی را ندارد.",
            "به Login مجوز کاربر دیتابیس بدهید یا نام دیتابیس را اصلاح کنید."),
        DatabaseSetupErrorCode.PermissionDenied => (
            "مجوز لازم برای این عملیات (مثلاً ساخت دیتابیس یا جدول) وجود ندارد.",
            "از مدیر SQL Server بخواهید مجوز CREATE DATABASE یا db_owner بدهد؛ یا دیتابیس را از پیش بسازد."),
        DatabaseSetupErrorCode.DatabaseFileError => (
            "SQL Server نتوانست فایل‌های دیتابیس را بسازد یا باز کند.",
            "مجوز پوشه داده SQL Server و نام فایل‌های موجود را بررسی کنید؛ برنامه هیچ فایل داده‌ای را حذف نمی‌کند."),
        DatabaseSetupErrorCode.DiskFull => (
            "فضای دیسک یا لاگ SQL Server پر است.",
            "فضای آزاد کنید و دوباره تلاش کنید؛ دیتابیس ناقص ساخته‌شده حذف نمی‌شود و تلاش دوباره امن است."),
        DatabaseSetupErrorCode.DatabaseMissing => (
            "اتصال موفق بود اما دیتابیس وجود ندارد.",
            "گزینه ساخت دیتابیس را فعال کنید یا نام دیتابیس موجود را وارد کنید."),
        DatabaseSetupErrorCode.MigrationFailed => (
            "ساخت یا به‌روزرسانی ساختار جدول‌ها (Migration) ناموفق بود.",
            "شناسه پیگیری را به پشتیبان بدهید؛ دیتابیس حذف نشده و تلاش دوباره امن است."),
        DatabaseSetupErrorCode.PendingMigrations => (
            "دیتابیس فرانو است اما چند Migration اعمال‌نشده دارد.",
            "پس از تهیه نسخه پشتیبان، گزینه «ادامه Migrationهای ناتمام» را فعال و دوباره تلاش کنید."),
        DatabaseSetupErrorCode.NotFaranoDatabase => (
            "این دیتابیس خالی نیست و ساختار آن با فرانو یکی نیست؛ هیچ تغییری روی آن داده نشد.",
            "نام دیتابیس خالی یا جدید انتخاب کنید."),
        DatabaseSetupErrorCode.DatabaseNewerThanApplication => (
            "پایگاه داده از نسخهٔ جدیدتر فرانو است؛ برنامه را به‌روزرسانی کنید.",
            "نسخهٔ برنامه را به نسخهٔ جدیدتر ارتقا دهید؛ هیچ تغییری روی دیتابیس داده نشد."),
        DatabaseSetupErrorCode.SystemDatabaseNotAllowed => (
            "این نام روی SQL Server به یکی از دیتابیس‌های سیستمی (master، model، msdb، tempdb) اشاره می‌کند و مجاز نیست؛ هیچ تغییری داده نشد.",
            "نام دیگری برای دیتابیس انتخاب کنید؛ مثلاً Farano."),
        DatabaseSetupErrorCode.AlreadyConfigured => (
            "تنظیمات اتصال از قبل ذخیره شده یا وضعیت آن تغییر کرده است؛ این درخواست اجرا نشد.",
            "برنامه را متوقف و دوباره اجرا کنید تا تنظیمات فعلی بارگذاری شود."),
        DatabaseSetupErrorCode.SettingsAccessDenied => (
            "پوشه تنظیمات برنامه قابل نوشتن نیست.",
            "دسترسی نوشتن حساب اجرای برنامه را بدهید یا مسیر را با Farano__Settings__Directory تغییر دهید."),
        DatabaseSetupErrorCode.SettingsIoError => (
            "ذخیره فایل تنظیمات ناموفق بود.",
            "فضای دیسک و دسترسی پوشه تنظیمات را بررسی کنید؛ وضعیت قبلی حفظ شده است."),
        DatabaseSetupErrorCode.SettingsProtectionFailed => (
            "رمزنگاری تنظیمات اتصال ناموفق بود.",
            "دسترسی به پوشه keys و حساب اجرای برنامه (DPAPI در ویندوز) را بررسی کنید."),
        DatabaseSetupErrorCode.SetupInProgress => (
            "راه‌اندازی دیگری در حال اجراست.",
            "چند لحظه صبر کنید و دوباره تلاش کنید."),
        _ => (
            "راه‌اندازی به دلیل خطای پیش‌بینی‌نشده ناموفق بود.",
            "گزارش سرور را با شناسه پیگیری بررسی کنید؛ اطلاعات اتصال در پیام نمایش داده نمی‌شود."),
    };
}
