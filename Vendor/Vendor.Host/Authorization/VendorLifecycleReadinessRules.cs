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

        stages.Add(s.ProvisioningStatus is null
            ? new LifecycleStage("signing", NotApplicable)
            : s.ProvisioningStatus == "AwaitingSigner"
                ? new LifecycleStage("signing", Blocked,
                    Reason: "امضای بسته Provisioning در دسترس نیست." +
                        (s.ProvisioningFailureCode is null ? string.Empty : $" ({s.ProvisioningFailureCode})"),
                    ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                    CorrectiveAction: "تأمین HSM/Signer واقعی را بررسی کنید (D-056/D-107).")
                : new LifecycleStage("signing", Ready, "بسته Provisioning امضا شده است.",
                    ChangedAtUtc: s.ProvisioningUpdatedAtUtc));

        stages.Add(s.LatestAmendmentStatus == "Issued"
            ? new LifecycleStage("signatureVerification", Ready,
                "کاندید Amendment از آخرین پاکت امضاشده و verify‌شده بازسازی شد.")
            : new LifecycleStage("signatureVerification", NotApplicable));

        stages.Add(s.ProvisioningStatus is "Signed" or "Confirmed" or "Rejected"
            ? new LifecycleStage("delivery", Ready, "بسته امضاشده برای خروجی و تحویل آماده است.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc)
            : s.ProvisioningStatus == "AwaitingSigner"
                ? new LifecycleStage("delivery", Pending, "تحویل پس از امضا انجام می‌شود.")
                : new LifecycleStage("delivery", NotApplicable));

        stages.Add(s.AcknowledgementResult is null
            ? (s.ProvisioningStatus == "Confirmed"
                ? new LifecycleStage("ack", Pending, "وضعیت Confirmed است اما نتیجه ACK ثبت نشده.")
                : new LifecycleStage("ack", NotApplicable))
            : s.AcknowledgementResult == "Applied"
                ? new LifecycleStage("ack", Ready, "پاسخ امضاشده مشتری «اعمال‌شده» تأیید شد.",
                    ChangedAtUtc: s.ProvisioningUpdatedAtUtc)
                : new LifecycleStage("ack", Failed,
                    Reason: $"پاسخ مشتری «{s.AcknowledgementResult}» بود." +
                        (s.ProvisioningFailureCode is null ? string.Empty : $" ({s.ProvisioningFailureCode})"),
                    ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                    CorrectiveAction: "علت رد در بسته را اصلاح و درخواست Provisioning جدید ثبت کنید."));

        stages.Add(s.HasInstallations
            ? new LifecycleStage("installation", Ready,
                $"نصب {s.LatestInstallationCode} با وضعیت {s.LatestInstallationStatus} ثبت شده است.",
                ReferenceId: s.LatestInstallationCode)
            : new LifecycleStage("installation", Pending, "نصبی برای این مشتری ثبت نشده است.",
                CorrectiveAction: "در صفحه مشتریان یک نصب ایجاد کنید."));

        stages.Add(s.AcknowledgementResult == "Applied"
            ? new LifecycleStage("bootstrap", Ready,
                "مدیر اولیه مشتری ایجاد شد؛ تکمیل Enrollment (تعیین رمز) در سمت مشتری انجام می‌شود.")
            : s.ProvisioningStatus == "Confirmed" && s.HasProvisioningRequest
                ? new LifecycleStage("bootstrap", Pending, "پاسخ مشتری هنوز «اعمال‌شده» نشده است.")
                : new LifecycleStage("bootstrap", NotApplicable));

        stages.Add(s.ProvisioningStatus switch
        {
            null => new LifecycleStage("provisioning", NotApplicable),
            "Confirmed" => new LifecycleStage("provisioning", Ready, "چرخه Provisioning با ACK معتبر بسته شد.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc),
            "PendingDelivery" => new LifecycleStage("provisioning", Pending,
                "بسته امضا شده اما هنوز تحویل/ACK نشده است.",
                CorrectiveAction: "بسته را Export و به مشتری تحویل دهید."),
            "Rejected" or "Failed" => new LifecycleStage("provisioning", Failed,
                Reason: "چرخه Provisioning با شکست بسته شد." +
                    (s.ProvisioningFailureCode is null ? string.Empty : $" ({s.ProvisioningFailureCode})"),
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc,
                CorrectiveAction: "علت را اصلاح و درخواست جدید ثبت کنید."),
            _ => new LifecycleStage("provisioning", Pending, $"وضعیت فعلی: {s.ProvisioningStatus}.",
                ChangedAtUtc: s.ProvisioningUpdatedAtUtc)
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
