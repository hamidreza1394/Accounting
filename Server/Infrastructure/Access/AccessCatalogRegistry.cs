using Faranou.Licensing.Contracts;

namespace Infrastructure.Access;

/// <summary>
/// کاتالوگ کد-محور زیرسیستم‌ها، منوها و فرم‌های واقعی فرانو (D-075).
/// مرجع سمت سرور برای PRهای Stage 2-D/2-F؛ محتوای آن با
/// docs/SUBSYSTEM_CATALOG.md و docs/MENU_FORM_CATALOG.md هم‌خوان نگه داشته می‌شود
/// و هر واگرایی یک باگ مستندات است.
/// قواعد: فقط-خواندنی؛ هیچ مجوزی نمی‌سازد (الگوی D-052)؛ کدهای entitlement فقط از
/// ثابت‌های <see cref="Faranou.Licensing.Contracts.LicenseSubsystemCodes"/> می‌آیند؛
/// هیچ جدول یا Migration جدیدی پشت این کاتالوگ نیست (D-075 بند ۵).
/// </summary>
public static class AccessCatalogRegistry
{
    public const string EntitlementScopeNone = "none";
    public const string EntitlementScopeInstallation = "installation";
    public const string EntitlementScopeCompany = "company";

    public const string MenuStatusActive = "active";
    public const string MenuStatusComingSoon = "coming-soon";

    public sealed record AccessSubsystem(
        string Code,
        string Title,
        string EntitlementScope,
        bool EntitlementLinked,
        string? EntitlementCode);

    public sealed record AccessMenu(
        string Label,
        string? Group,
        string SubsystemCode,
        string Status,
        string? Route);

    public sealed record AccessForm(
        string Route,
        string Title,
        string SubsystemCode,
        string? PermissionHint,
        string Parent);

    /// <summary>زیرسیستم‌های واقعی مخزن (SUBSYSTEM_CATALOG.md §2)؛ مرتب بر اساس کد.</summary>
    public static IReadOnlyList<AccessSubsystem> Subsystems { get; } =
    [
        new("access-admin", "گروه‌های دسترسی و کاتالوگ مجوز", EntitlementScopeNone, false, null),
        new("core", "هسته — داشبورد و زمینهٔ کاری", EntitlementScopeNone, false, null),
        new("corporate-groups", "گروه شرکتی و تلفیق", EntitlementScopeNone, false, null),
        new("fiscal", "دوره و سال مالی", EntitlementScopeNone, false, null),
        new("hr", "ساختار سازمانی و منابع انسانی", EntitlementScopeNone, false, null),
        new("identity-admin", "مدیریت کاربران نصب", EntitlementScopeNone, false, null),
        new("infrastructure-operator", "ابزار اپراتور نصب", EntitlementScopeNone, false, null),
        new("licensing", "لایسنس و فعال‌سازی (سمت مشتری)", EntitlementScopeNone, false, null),
        new("membership", "عضویت شرکت و گردش درخواست", EntitlementScopeNone, false, null),
        new("reporting", "گزارش‌ها", EntitlementScopeNone, false, null),
        new("security-audit", "امنیت حساب، نشست و گزارش امنیتی", EntitlementScopeNone, false, null),
        new("support", "راهنما و پشتیبانی", EntitlementScopeNone, false, null),
        new("vendor", "سامانهٔ مرکزی فروشنده", EntitlementScopeNone, false, null),
        new(LicenseSubsystemCodes.Finance, "حسابداری", EntitlementScopeInstallation, true, LicenseSubsystemCodes.Finance),
        new(LicenseSubsystemCodes.Inventory, "انبار", EntitlementScopeInstallation, true, LicenseSubsystemCodes.Inventory),
        new(LicenseSubsystemCodes.Sales, "فروش", EntitlementScopeInstallation, true, LicenseSubsystemCodes.Sales),
        new(LicenseSubsystemCodes.Treasury, "خزانه‌داری", EntitlementScopeInstallation, true, LicenseSubsystemCodes.Treasury),
        new(LicenseSubsystemCodes.TradeImport, "بازرگانی خارجی", EntitlementScopeCompany, true, LicenseSubsystemCodes.TradeImport),
    ];

    /// <summary>درخت منوی واقعی Sidebar (MENU_FORM_CATALOG.md §1)؛ Status=active فقط برای صفحات واقعی.</summary>
    public static IReadOnlyList<AccessMenu> Menus { get; } =
    [
        new("داشبورد", null, "core", MenuStatusActive, "/"),
        new("حسابداری", "حسابداری و مالی", LicenseSubsystemCodes.Finance, MenuStatusComingSoon, null),
        new("خزانه‌داری", "حسابداری و مالی", LicenseSubsystemCodes.Treasury, MenuStatusComingSoon, null),
        new("فروش", "فروش، بازرگانی و انبار", LicenseSubsystemCodes.Sales, MenuStatusComingSoon, null),
        new("بازرگانی خارجی", "فروش، بازرگانی و انبار", LicenseSubsystemCodes.TradeImport, MenuStatusComingSoon, null),
        new("انبار", "فروش، بازرگانی و انبار", LicenseSubsystemCodes.Inventory, MenuStatusComingSoon, null),
        new("گزارش‌ها", "گزارش‌ها و اطلاعات پایه", "reporting", MenuStatusComingSoon, null),
        new("اطلاعات پایه", "گزارش‌ها و اطلاعات پایه", "reporting", MenuStatusComingSoon, null),
        new("واحدهای سازمانی", "منابع انسانی", "hr", MenuStatusActive, "/hr/org/units"),
        new("سمت‌های سازمانی", "منابع انسانی", "hr", MenuStatusActive, "/hr/org/positions"),
        new("کارکنان", "منابع انسانی", "hr", MenuStatusActive, "/hr/org/employees"),
        new("مدیریت کاربران", "مدیریت سیستم", "identity-admin", MenuStatusActive, "/admin/users"),
        new("کاربران شرکت جاری", "مدیریت سیستم", "membership", MenuStatusActive, "/company/users"),
        new("گروه‌های دسترسی", "مدیریت سیستم", "access-admin", MenuStatusActive, "/admin/roles"),
        new("کاتالوگ و جزئیات مجوزها", "مدیریت سیستم", "access-admin", MenuStatusActive, "/admin/permissions"),
        new("درخواست‌های عضویت", "مدیریت سیستم", "membership", MenuStatusActive, "/admin/membership-requests"),
        new("درخواست‌های عضویت من", "مدیریت سیستم", "membership", MenuStatusActive, "/account/membership-requests"),
        new("گزارش امنیت و سوابق", "مدیریت سیستم", "security-audit", MenuStatusActive, "/admin/security/audit"),
        new("سیاست‌های امنیتی", "مدیریت سیستم", "security-audit", MenuStatusComingSoon, null),
        new("راهنما و پشتیبانی", null, "support", MenuStatusComingSoon, null),
    ];

    /// <summary>همهٔ Routeهای واقعی روتر (MENU_FORM_CATALOG.md §2)؛ مسیرها یکتا هستند.</summary>
    public static IReadOnlyList<AccessForm> Forms { get; } =
    [
        new("/", "داشبورد", "core", null, "منو — داشبورد"),
        new("/admin/users", "مدیریت کاربران", "identity-admin", "admin.users.view", "منو — مدیریت سیستم"),
        new("/admin/users/new", "ایجاد کاربر", "identity-admin", "admin.users.create", "فرم — مدیریت کاربران"),
        new("/admin/users/:id", "جزئیات کاربر", "identity-admin", "admin.users.view", "فرم — مدیریت کاربران"),
        new("/admin/users/:id/edit", "ویرایش کاربر", "identity-admin", "admin.users.update", "فرم — جزئیات کاربر"),
        new("/admin/permissions", "کاتالوگ مجوزها", "access-admin", "admin.permissions.view", "منو — مدیریت سیستم"),
        new("/admin/roles", "گروه‌های دسترسی", "access-admin", "admin.roles.view", "منو — مدیریت سیستم"),
        new("/admin/roles/new", "ایجاد گروه دسترسی", "access-admin", "admin.roles.create", "فرم — گروه‌های دسترسی"),
        new("/admin/roles/:roleId", "جزئیات گروه دسترسی", "access-admin", "admin.roles.view", "فرم — گروه‌های دسترسی"),
        new("/company/users", "کاربران شرکت", "membership", null, "منو — مدیریت سیستم"),
        new("/admin/security/audit", "گزارش امنیت و سوابق", "security-audit", "admin.security.view", "منو — مدیریت سیستم"),
        new("/admin/membership-requests", "درخواست‌های عضویت", "membership", null, "منو — مدیریت سیستم"),
        new("/account/membership-requests", "درخواست‌های عضویت من", "membership", "account.view", "منو — مدیریت سیستم"),
        new("/hr/org/units", "واحدهای سازمانی", "hr", "hr.units.view", "منو — منابع انسانی"),
        new("/hr/org/units/new", "ایجاد واحد سازمانی", "hr", "hr.units.view", "فرم — واحدهای سازمانی"),
        new("/hr/org/units/:unitId", "جزئیات واحد سازمانی", "hr", "hr.units.view", "فرم — واحدهای سازمانی"),
        new("/hr/org/positions", "سمت‌های سازمانی", "hr", "hr.positions.view", "منو — منابع انسانی"),
        new("/hr/org/positions/new", "ایجاد سمت سازمانی", "hr", "hr.positions.view", "فرم — سمت‌های سازمانی"),
        new("/hr/org/positions/:positionId", "جزئیات سمت", "hr", "hr.positions.view", "فرم — سمت‌های سازمانی"),
        new("/hr/org/employees", "کارکنان", "hr", "hr.employees.view", "منو — منابع انسانی"),
        new("/hr/org/employees/new", "ثبت کارمند", "hr", "hr.employees.view", "فرم — کارکنان"),
        new("/hr/org/employees/:employeeId", "پروندهٔ کارمند", "hr", "hr.employees.view", "فرم — کارکنان"),
        new("/hr/org/employees/:employeeId/edit", "ویرایش کارمند", "hr", "hr.employees.view", "فرم — پروندهٔ کارمند"),
    ];
}
