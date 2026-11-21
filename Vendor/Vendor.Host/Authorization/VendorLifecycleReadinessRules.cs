namespace Faranou.Vendor.Host.Authorization;

/// <summary>
/// Pure readiness rules for the vendor customer lifecycle (D-108/D-109 context, #392 aware).
/// Every stage status is derived from a snapshot of REAL persisted data — nothing is inferred
/// from a single successful request, a failed stage is never hidden by an earlier success, and
/// no secrets or private keys are ever part of the snapshot or the outcome. The endpoint that
/// feeds the snapshot is read-only.
/// </summary>
public static class VendorLifecycleReadinessRules
{
    public const string Ready = "Ready";
    public const string Pending = "Pending";
    public const string Blocked = "Blocked";
    public const string Failed = "Failed";
    public const string NotApplicable = "NotApplicable";

    public sealed record LifecycleStage(
        string Key,
        string Status,
        string? Reason = null,
        string? ReferenceId = null,
        DateTimeOffset? ChangedAtUtc = null,
        string? CorrectiveAction = null);

    /// <summary>Persisted facts the rules read; loaded by the endpoint with narrow queries.</summary>
    public sealed record LifecycleSnapshot(
        string CustomerStatus,
        bool HasOrder,
        string? LatestOrderNumber,
        DateTimeOffset? LatestOrderAtUtc,
        int VerifiedPayments,
        int RecordedPayments,
        int RejectedPayments,
        int IssuedLicenses,
        int DraftLicenses,
        bool HasInstallations,
        string? LatestInstallationCode,
        string? LatestInstallationStatus,
        DateTimeOffset? InstallationLastSeenAtUtc,
        bool HasProvisioningRequest,
        string? ProvisioningStatus,
        string? ProvisioningFailureCode,
        string? AcknowledgementResult,
        DateTimeOffset? ProvisioningUpdatedAtUtc,
        DateTimeOffset? ProvisioningDeliveredAtUtc,
        int PendingApprovals,
        string? LatestAmendmentStatus,
        string? LatestAmendmentFailureCode,
        DateTimeOffset? LatestAmendmentUpdatedAtUtc,
        bool CompanyReferenceGateEnabled);

    public static IReadOnlyList<LifecycleStage> Resolve(LifecycleSnapshot s)
    {
        var stages = new List<LifecycleStage>(14);

        stages.Add(new LifecycleStage("customerRegistration",
            string.Equals(s.CustomerStatus, "Active", StringComparison.Ordinal) ? Ready : Blocked,
            string.Equals(s.CustomerStatus, "Active", StringComparison.Ordinal) ? null : $"وضعیت مشتری {s.CustomerStatus} است.",
            CorrectiveAction: string.Equals(s.CustomerStatus, "Active", StringComparison.Ordinal)
                ? null : "وضعیت مشتری را در صفحه مشتریان فعال کنید."));

        stages.Add(s.HasOrder
            ? new LifecycleStage("commercialOrder", Ready,
                ReferenceId: s.LatestOrderNumber, ChangedAtUtc: s.LatestOrderAtUtc)
            : new LifecycleStage("commercialOrder", Pending, "هنوز سفارشی برای این مشتری ثبت نشده است.",
                CorrectiveAction: "در صفحه «فروش و پرداخت» یک سفارش ثبت کنید."));

        stages.Add(!s.HasOrder
            ? new LifecycleStage("payment", NotApplicable, "بدون سفارش، پرداخت موضوعیت ندارد.")
            : s.VerifiedPayments > 0
                ? new LifecycleStage("payment", Ready,
                    $"{s.VerifiedPayments} پرداخت تأییدشده؛ مانده بدهی مستقل از Amendment است.",
                    ChangedAtUtc: s.LatestOrderAtUtc)
                : s.RecordedPayments > 0
                    ? new LifecycleStage("payment", Pending, "پرداخت ثبت شده اما هنوز تأیید مستقل نشده است.",
                        CorrectiveAction: "درخواست تأیید پرداخت را از صفحه «فروش و پرداخت» ارسال کنید.")
                    : s.RejectedPayments > 0
                        ? new LifecycleStage("payment", Pending, "پرداخت رد شده است؛ وضعیت همچنان مستقل از Amendment است.",
                            CorrectiveAction: "پرداخت اصلاح‌شده جدیدی ثبت کنید.")
                        : new LifecycleStage("payment", Pending, "هیچ پرداختی ثبت نشده است.",
                            CorrectiveAction: "پرداخت را با مرجع رسید ثبت کنید."));

        stages.Add(s.PendingApprovals > 0
            ? new LifecycleStage("issuanceApproval", Pending,
                $"{s.PendingApprovals} درخواست تأیید در انتظار تصمیم است.",
                CorrectiveAction: "در صفحه «درخواست‌های تأیید» تصمیم بگیرید.")
            : new LifecycleStage("issuanceApproval", NotApplicable));

        stages.Add(s.IssuedLicenses > 0
            ? new LifecycleStage("entitlement", Ready, $"{s.IssuedLicenses} لایسنس صادرشده فعال است.")
            : s.DraftLicenses > 0
                ? new LifecycleStage("entitlement", Pending, "لایسنس پیش‌نویس موجود است اما صادر نشده.",
                    CorrectiveAction: "درخواست صدور لایسنس را ثبت و تأیید کنید.")
                : new LifecycleStage("entitlement", Pending, "لایسنسی برای این مشتری وجود ندارد.",
                    CorrectiveAction: "یک لایسنس ایجاد و صادر کنید."));

        stages.Add(s.LatestAmendmentStatus is null
            ? new LifecycleStage("amendment", NotApplicable)
            : s.LatestAmendmentStatus switch
            {
                "Issued" => new LifecycleStage("amendment", Ready, "آخرین Amendment امضا و صادر شد.",
                    ChangedAtUtc: s.LatestAmendmentUpdatedAtUtc),
                "Requested" or "PendingApproval" => new LifecycleStage("amendment", Pending,
                    "درخواست Amendment در جریان است.",
                    ChangedAtUtc: s.LatestAmendmentUpdatedAtUtc,
                    CorrectiveAction: "درخواست را برای تأیید ارسال یا در صفحه تأییدها تصمیم بگیرید."),
                "Rejected" => new LifecycleStage("amendment", Failed,
                    "آخرین Amendment رد شد؛ وضعیت پرداخت مستقل باقی مانده است.",
                    ChangedAtUtc: s.LatestAmendmentUpdatedAtUtc,
                    CorrectiveAction: "درخواست اصلاح‌شده جدیدی برای همان ردیف ثبت کنید."),
                _ => new LifecycleStage("amendment", Failed,
                    $"وضعیت Amendment نامعتبر است ({s.LatestAmendmentStatus}).",
                    CorrectiveAction: "با پشتیبانی فنی بررسی کنید.")
            });

        stages.Add(s.ProvisioningStatus switch
        {
            null => new LifecycleStage("signing", NotApplicable),
            "AwaitingSigner" => new LifecycleStage("signing", Pending,
                "بسته هنوز با موفقیت امضا نشده است.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                CorrectiveAction: "درخواست تأیید را بررسی کنید؛ در صورت خطای امضا، signer پیکربندی‌شده را بررسی کنید (D-107)."),
            "PendingDelivery" or "Confirmed" or "Failed" => new LifecycleStage("signing", Ready,
                "بسته Provisioning با موفقیت امضا شده است.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc),
            _ => new LifecycleStage("signing", Failed,
                $"وضعیت Provisioning برای مرحله امضا ناشناخته است ({s.ProvisioningStatus}).",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                CorrectiveAction: "وضعیت ذخیره‌شده را بررسی کنید؛ از فرض موفقیت امضا خودداری کنید.")
        });

        stages.Add(s.LatestAmendmentStatus == "Issued"
            ? new LifecycleStage("signatureVerification", Ready,
                "کاندید Amendment از آخرین پاکت امضاشده و verify‌شده بازسازی شد.")
            : s.ProvisioningStatus is "PendingDelivery" or "Confirmed" or "Failed"
                ? new LifecycleStage("signatureVerification", Ready,
                    "امضای بسته Provisioning پیش از ثبت وضعیت نهایی verify شده است.")
                : new LifecycleStage("signatureVerification", NotApplicable));

        stages.Add(s.ProvisioningStatus switch
        {
            null => new LifecycleStage("delivery", NotApplicable),
            "AwaitingSigner" => new LifecycleStage("delivery", Pending,
                "تحویل پس از تکمیل امضا امکان‌پذیر است.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc),
            "PendingDelivery" when s.ProvisioningDeliveredAtUtc is null =>
                new LifecycleStage("delivery", Pending,
                    "بسته امضا شده اما هنوز زمان تحویل ثبت نشده است.",
                    ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                    CorrectiveAction: "بسته را از مسیر مجاز Export و به مشتری تحویل دهید."),
            "PendingDelivery" => new LifecycleStage("delivery", Ready,
                "زمان تحویل بسته ثبت شده؛ پاسخ ACK مشتری در انتظار است.",
                ChangedAtUtc: s.ProvisioningDeliveredAtUtc),
            "Confirmed" or "Failed" => new LifecycleStage("delivery", Ready,
                "بسته تحویل شده و پاسخ مشتری ثبت شده است.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc),
            _ => new LifecycleStage("delivery", Failed,
                $"وضعیت Provisioning برای مرحله تحویل ناشناخته است ({s.ProvisioningStatus}).",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                CorrectiveAction: "وضعیت ذخیره‌شده و تاریخچه تحویل را بررسی کنید.")
        });

        stages.Add(s.AcknowledgementResult switch
        {
            "Applied" or "AlreadyApplied" => new LifecycleStage("ack", Ready,
                "پاسخ امضاشده مشتری اعمال یا قبلاً اعمال‌شده تأیید شد.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc),
            "Rejected" => new LifecycleStage("ack", Failed,
                Reason: $"پاسخ مشتری «Rejected» بود." +
                    (s.ProvisioningFailureCode is null ? string.Empty : $" ({s.ProvisioningFailureCode})"),
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                CorrectiveAction: "علت رد در بسته را اصلاح و درخواست Provisioning جدید ثبت کنید."),
            null when s.ProvisioningDeliveredAtUtc is not null =>
                new LifecycleStage("ack", Pending,
                    "بسته تحویل شده اما ACK معتبر مشتری هنوز ثبت نشده است.",
                    ChangedAtUtc: s.ProvisioningDeliveredAtUtc,
                    CorrectiveAction: "پس از Import در میزبان مشتری، ACK امضاشده را دریافت کنید."),
            null when s.ProvisioningStatus is "Confirmed" or "Failed" =>
                new LifecycleStage("ack", Failed,
                    "وضعیت نهایی Provisioning بدون نتیجه ACK ذخیره شده است.",
                    ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                    CorrectiveAction: "سازگاری وضعیت و رکورد ACK را بررسی کنید."),
            null => new LifecycleStage("ack", NotApplicable),
            _ => new LifecycleStage("ack", Failed,
                $"نتیجه ACK ناشناخته است ({s.AcknowledgementResult}).",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                CorrectiveAction: "نتیجه ACK ذخیره‌شده را بررسی کنید.")
        });

        stages.Add(!s.HasInstallations
            ? new LifecycleStage("installation", Pending, "نصبی برای این مشتری ثبت نشده است.",
                CorrectiveAction: "در صفحه مشتریان یک نصب ایجاد کنید.")
            : s.LatestInstallationStatus switch
            {
                "Active" => new LifecycleStage("installation", Ready,
                    $"نصب {s.LatestInstallationCode} فعال است.",
                    ReferenceId: s.LatestInstallationCode),
                "Suspended" => new LifecycleStage("installation", Blocked,
                    $"نصب {s.LatestInstallationCode} تعلیق شده است.",
                    ReferenceId: s.LatestInstallationCode,
                    CorrectiveAction: "پس از رفع علت تعلیق، وضعیت نصب را از مسیر تأیید مجاز فعال کنید."),
                _ => new LifecycleStage("installation", Failed,
                    $"وضعیت نصب ناشناخته یا نامعتبر است ({s.LatestInstallationStatus ?? "null"}).",
                    ReferenceId: s.LatestInstallationCode,
                    CorrectiveAction: "وضعیت ذخیره‌شده نصب را بررسی کنید.")
            });

        stages.Add(s.AcknowledgementResult is "Applied" or "AlreadyApplied"
            ? new LifecycleStage("bootstrap", Ready,
                "مدیر اولیه مشتری ایجاد شد؛ تکمیل Enrollment (تعیین رمز) در سمت مشتری انجام می‌شود.")
            : s.AcknowledgementResult == "Rejected"
                ? new LifecycleStage("bootstrap", Failed,
                    "درخواست راه‌اندازی توسط مشتری رد شده و Bootstrap تکمیل نشده است.",
                    ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                    CorrectiveAction: "علت رد را رفع و درخواست Provisioning جدید ثبت کنید.")
                : s.ProvisioningDeliveredAtUtc is not null && s.AcknowledgementResult is null
                    ? new LifecycleStage("bootstrap", Pending,
                        "بسته تحویل شده؛ تکمیل Bootstrap پس از اعمال بسته و ACK معتبر تأیید می‌شود.")
                    : new LifecycleStage("bootstrap", NotApplicable));

        stages.Add(s.ProvisioningStatus switch
        {
            null => new LifecycleStage("provisioning", NotApplicable),
            "AwaitingSigner" => new LifecycleStage("provisioning", Pending,
                "درخواست در انتظار تأیید و امضای بسته است.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc),
            "PendingDelivery" when s.ProvisioningDeliveredAtUtc is null =>
                new LifecycleStage("provisioning", Pending,
                    "بسته امضا شده اما هنوز تحویل نشده است.",
                    ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                    CorrectiveAction: "بسته را از مسیر مجاز Export و به مشتری تحویل دهید."),
            "PendingDelivery" => new LifecycleStage("provisioning", Pending,
                "بسته تحویل شده و منتظر ACK معتبر مشتری است.",
                ChangedAtUtc: s.ProvisioningDeliveredAtUtc,
                CorrectiveAction: "پس از Import، ACK امضاشده مشتری را دریافت کنید."),
            "Confirmed" => new LifecycleStage("provisioning", Ready,
                "چرخه Provisioning با ACK معتبر بسته شد.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc),
            "Failed" => new LifecycleStage("provisioning", Failed,
                Reason: "چرخه Provisioning با پاسخ ردشده مشتری بسته شد." +
                    (s.ProvisioningFailureCode is null ? string.Empty : $" ({s.ProvisioningFailureCode})"),
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                CorrectiveAction: "علت را اصلاح و درخواست جدید ثبت کنید."),
            _ => new LifecycleStage("provisioning", Failed,
                $"وضعیت Provisioning ناشناخته است ({s.ProvisioningStatus}).",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                CorrectiveAction: "وضعیت ذخیره‌شده را بررسی کنید؛ آن را Pending یا Ready فرض نکنید.")
        });

        stages.Add(s.CompanyReferenceGateEnabled
            ? new LifecycleStage("runtimeGate", Ready,
                "گیت CompanyReference باز است؛ entitlement شرکت‌محور فقط با mapping معتبر اجرا می‌شود.")
            : new LifecycleStage("runtimeGate", Blocked,
                "گیت CompanyReference بسته است (#392): فقط اقلام Installation-scoped (maxCompanies، دوره پشتیبانی/به‌روزرسانی) قابل صدور هستند و Company-scoped هرگز Ready اعلام نمی‌شود.",
                CorrectiveAction: "پس از حل #392 و تأیید مالک، گیت را با تنظیمات فعال کنید."));

        return stages;
    }
}
