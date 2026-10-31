using System.Collections.ObjectModel;

namespace Faranou.Modules.Trade.Application.Authorization;

/// <summary>
/// A permission owned by the Trade module but stored in the shared company-role permission catalog.
/// Commercial module entitlement is checked separately from these operational permissions.
/// </summary>
public sealed record TradePermissionDescriptor(
    string Code,
    string Name,
    string Description,
    TradeAccessIntent AccessIntent);

/// <summary>
/// The initial, version-controlled Trade permission catalog. It is intentionally limited to
/// the first approved forms; later workflow forms add permissions in their own reviewed issues.
/// </summary>
public static class TradePermissionCatalog
{
    private static readonly ReadOnlyCollection<TradePermissionDescriptor> Permissions =
        Array.AsReadOnly(Build());

    public static IReadOnlyList<TradePermissionDescriptor> All => Permissions;

    private static TradePermissionDescriptor[] Build()
    {
        var permissions = new List<TradePermissionDescriptor>();

        AddForm(permissions, "party", "طرف حساب", includeUpdate: true, includeApprove: false, includeDelete: true);
        AddForm(permissions, "product", "کالا", includeUpdate: true, includeApprove: false, includeDelete: true);
        AddForm(permissions, "purchase-request", "درخواست خرید", includeUpdate: true, includeApprove: true, includeDelete: true);
        AddForm(permissions, "supplier-inquiry", "استعلام و پیشنهاد تأمین‌کننده", includeUpdate: true, includeApprove: true, includeDelete: true);
        AddForm(permissions, "quotation-comparison", "مقایسه پیشنهادها", includeUpdate: false, includeApprove: true, includeDelete: false);
        AddForm(permissions, "proforma", "پیش‌فاکتور", includeUpdate: true, includeApprove: true, includeDelete: true);
        AddForm(permissions, "contract", "قرارداد", includeUpdate: true, includeApprove: true, includeDelete: true);
        AddForm(permissions, "import-case", "پرونده واردات", includeUpdate: true, includeApprove: true, includeDelete: false, includeClose: true);

        Add(
            permissions,
            TradePermissionCodes.CompanyReportView,
            "مشاهده گزارش بازرگانی شرکت",
            "اجازه مشاهده گزارش‌های عملیاتی بازرگانی در شرکت جاری.",
            TradeAccessIntent.Read);
        Add(
            permissions,
            TradePermissionCodes.MultiCompanyReportView,
            "مشاهده گزارش عملیاتی چندشرکتی بازرگانی",
            "اجازه مشاهده گزارش عملیاتی در تمام شرکت‌ها و دوره‌های مجاز؛ دسترسی به همه محدوده باید یکجا تأیید شود.",
            TradeAccessIntent.Read);

        Validate(permissions);
        return permissions.ToArray();
    }

    private static void AddForm(
        ICollection<TradePermissionDescriptor> permissions,
        string formCode,
        string formName,
        bool includeUpdate,
        bool includeApprove,
        bool includeDelete,
        bool includeClose = false)
    {
        Add(permissions, TradePermissionCodes.FormView(formCode), $"مشاهده {formName}", $"اجازه مشاهده {formName} در ماژول بازرگانی خارجی.", TradeAccessIntent.Read);
        Add(permissions, TradePermissionCodes.FormCreate(formCode), $"ایجاد {formName}", $"اجازه ایجاد {formName} در ماژول بازرگانی خارجی.", TradeAccessIntent.Write);

        if (includeUpdate)
        {
            Add(permissions, TradePermissionCodes.FormUpdate(formCode), $"ویرایش {formName}", $"اجازه ویرایش {formName} در ماژول بازرگانی خارجی.", TradeAccessIntent.Write);
        }

        if (includeApprove)
        {
            Add(permissions, TradePermissionCodes.FormApprove(formCode), $"تأیید {formName}", $"اجازه تأیید {formName} در ماژول بازرگانی خارجی.", TradeAccessIntent.Write);
        }

        if (includeDelete)
        {
            Add(permissions, TradePermissionCodes.FormDelete(formCode), $"حذف {formName}", $"اجازه حذف {formName} در ماژول بازرگانی خارجی، مشروط به قواعد چرخه عمر.", TradeAccessIntent.Write);
        }

        if (includeClose)
        {
            Add(permissions, TradePermissionCodes.FormClose(formCode), $"بستن {formName}", $"اجازه بستن {formName} در ماژول بازرگانی خارجی پس از بررسی قواعد چرخه عمر.", TradeAccessIntent.Write);
        }

        Add(permissions, TradePermissionCodes.FormExport(formCode), $"خروجی {formName}", $"اجازه خروجی {formName} در ماژول بازرگانی خارجی.", TradeAccessIntent.Read);
    }

    private static void Add(
        ICollection<TradePermissionDescriptor> permissions,
        string code,
        string name,
        string description,
        TradeAccessIntent accessIntent)
    {
        permissions.Add(new TradePermissionDescriptor(code, name, description, accessIntent));
    }

    private static void Validate(IReadOnlyCollection<TradePermissionDescriptor> permissions)
    {
        if (permissions.Select(permission => permission.Code).Distinct(StringComparer.Ordinal).Count() != permissions.Count)
        {
            throw new InvalidOperationException("Trade permission codes must be unique.");
        }

        foreach (var permission in permissions)
        {
            if (permission.Code.Length > 120
                || permission.Code.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_')
                || string.IsNullOrWhiteSpace(permission.Name)
                || permission.Name.Length > 160
                || string.IsNullOrWhiteSpace(permission.Description)
                || permission.Description.Length > 500
                || !TradePermissionCodes.IsAccessIntentCompatible(permission.Code, permission.AccessIntent))
            {
                throw new InvalidOperationException($"Invalid Trade permission catalog entry: {permission.Code}");
            }
        }
    }
}
