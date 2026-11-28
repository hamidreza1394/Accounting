namespace Faranou.Vendor.Host;

/// <summary>
/// Least-privilege rules for the Development provisioning runtime identity, kept as pure,
/// unit-tested decisions: which server-level state disqualifies a login from being the
/// runtime identity (NO server role membership of any kind, no granted server permission
/// beyond CONNECT SQL, no CONNECT SQL deny) and which cross-database user mappings must be
/// refused. The SQL-side queries that feed these decisions live in the provisioning
/// pipeline; nothing here talks to SQL Server, so every rule can be tested without a server.
/// </summary>
internal static class VendorDevelopmentRuntimeIdentity
{
    /// <summary>
    /// Server-level disqualification rules for a runtime identity. NO server role is
    /// acceptable — fixed or instance-created (SQL Server 2022 ships extra ##MS_…## roles
    /// and operators can define their own) — and no granted server permission beyond CONNECT
    /// SQL. A deny is restrictive and normally fine (the flow itself denies VIEW ANY
    /// DATABASE on fresh logins), but a CONNECT SQL deny makes the login unusable at all.
    /// </summary>
    internal static class VendorRuntimeIdentity
    {
        private const string ConnectSqlPermission = "CONNECT SQL";

        public static bool HasExcessiveServerAccess(
            IEnumerable<string> roleNames,
            IEnumerable<string> grantedPermissions,
            IEnumerable<string> deniedPermissions,
            out string detail)
        {
            var roles = roleNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToList();
            var grants = grantedPermissions
                .Where(permission => !string.Equals(permission, ConnectSqlPermission, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var denies = deniedPermissions
                .Where(permission => string.Equals(permission, ConnectSqlPermission, StringComparison.OrdinalIgnoreCase))
                .ToList();
            detail = string.Join("، ", roles.Concat(grants).Concat(denies)
                .Distinct(StringComparer.OrdinalIgnoreCase));
            return roles.Count > 0 || grants.Count > 0 || denies.Count > 0;
        }
    }

    /// <summary>
    /// Cross-database mapping rule: a least-privilege runtime identity must not have a user
    /// mapping in any database other than the Vendor target, because that is exactly how
    /// access would leak beyond the vendor database. Returns the refusal detail listing the
    /// other databases so the operator can fix the mapping with the server administrator.
    /// </summary>
    internal static class VendorCrossDatabaseAccess
    {
        public static (bool Allowed, string? Refusal) EvaluateUserMappings(
            string loginName,
            IEnumerable<string> otherDatabaseNames)
        {
            var others = otherDatabaseNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (others.Count == 0)
            {
                return (true, null);
            }

            return (false, $"هویت Runtime «{loginName}» در دیتابیس‌های دیگری هم User است ({string.Join("، ", others)}) و به‌عنوان هویت کم‌اختیار پذیرفته نمی‌شود؛ mappingهای اضافه را با مدیر سرور حذف کنید یا نام Runtime دیگری انتخاب کنید.");
        }

        /// <summary>
        /// Audit-coverage rule: reusing an existing login is only allowed when the setup
        /// identity could actually audit EVERY non-target database on the instance. Without
        /// sysadmin/CONTROL SERVER/VIEW ANY DATABASE the enumeration itself is incomplete
        /// (unseen databases may hold a mapping), and a database the setup identity cannot
        /// open (offline, no user access) cannot be checked at all — its mapping survives
        /// invisibly and comes back with the database. Both gaps must refuse reuse (fail
        /// closed) instead of counting as "no mapping found".
        /// </summary>
        public static (bool Allowed, string? Refusal) EvaluateAuditCoverage(
            string loginName,
            bool canEnumerateAllDatabases,
            IEnumerable<string> unauditableDatabaseNames)
        {
            if (!canEnumerateAllDatabases)
            {
                return (false, $"هویت پیکربندی نمی‌تواند همهٔ دیتابیس‌های سرور را فهرست کند (نه sysadmin است و نه مجوز VIEW ANY DATABASE دارد)، پس نبودِ mapping اضافه برای Login «{loginName}» قابل اثبات نیست و استفاده از آن Login موجود پذیرفته نمی‌شود؛ دسترسی هویت پیکربندی را کامل کنید یا نام Runtime دیگری انتخاب کنید.");
            }

            var unauditable = unauditableDatabaseNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (unauditable.Count > 0)
            {
                return (false, $"ممیزی کامل دیتابیس‌های ({string.Join("، ", unauditable)}) ممکن نیست (Offline یا بدون دسترسی هویت پیکربندی)، پس استفاده از Login موجود «{loginName}» پذیرفته نمی‌شود؛ mapping این Login در آن دیتابیس‌ها می‌توانست پنهان بماند و با Online شدن دوباره فعال شود.");
            }

            return (true, null);
        }
    }
}
