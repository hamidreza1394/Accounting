using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Faranou.Vendor.Host;

/// <summary>
/// Safety primitives for the Development database provisioning flow, kept separate from the
/// pipeline so the fail-closed rules can be unit-tested and re-used without SQL access:
/// SQL identifier quoting, database and login name validation, the local SQL target
/// allow-list, the Vendor ownership marker (with value validation) and the controlled
/// recovery ledger for the window between CREATE DATABASE and the ownership marker write.
/// </summary>
internal static class VendorDevelopmentDatabaseSafety
{
    /// <summary>
    /// Bracket-quotes a SQL identifier that has already passed validation, escaping any
    /// closing bracket as defense in depth. Identifiers are never concatenated raw into SQL.
    /// </summary>
    internal static class VendorSqlIdentifier
    {
        public static string Quote(string identifier) =>
            $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    /// <summary>
    /// Database identifier validation, mirroring the verified customer-side rules: letters,
    /// digits, underscore, hyphen, dot and ordinary space in the middle only; no control or
    /// format characters (ZWNJ included); maximum 123 characters so the <c>_log</c> file name
    /// still fits the 128-character SQL Server limit; system names rejected after NFKC
    /// normalization so look-alike homoglyph names like full-width «ｍａｓｔｅｒ» cannot pass.
    /// </summary>
    internal static class VendorDatabaseName
    {
        private const int MaximumLength = 123;
        private static readonly HashSet<string> SystemNames = new(StringComparer.OrdinalIgnoreCase)
            { "master", "model", "msdb", "tempdb" };

        public static bool TryValidate(string? input, out string? validated, out string? error)
        {
            validated = null;
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "نام دیتابیس لازم است.";
                return false;
            }

            if (input.Length > MaximumLength)
            {
                error = $"نام دیتابیس حداکثر {MaximumLength} نویسه می‌تواند باشد.";
                return false;
            }

            if (char.IsWhiteSpace(input[0]) || char.IsWhiteSpace(input[^1]) ||
                input[0] == '.' || input[^1] == '.')
            {
                error = "نام دیتابیس نمی‌تواند با نقطه یا فاصله شروع یا پایان یابد.";
                return false;
            }

            foreach (var character in input)
            {
                if (char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format)
                {
                    error = "نام دیتابیس نمی‌تواند نویسهٔ کنترلی یا قالب‌بندی داشته باشد.";
                    return false;
                }

                if (!(char.IsLetter(character) || char.IsDigit(character) ||
                      character is '_' or '-' or '.' or ' '))
                {
                    error = "نام دیتابیس فقط می‌تواند حرف، رقم، «_»، «-»، «.» و فاصله داشته باشد.";
                    return false;
                }
            }

            if (input.Contains("..", StringComparison.Ordinal))
            {
                error = "نام دیتابیس نمی‌تواند «..» متوالی داشته باشد.";
                return false;
            }

            var normalized = input.Normalize(NormalizationForm.FormKC);
            if (SystemNames.Contains(normalized))
            {
                error = "نام دیتابیس سیستمی مجاز نیست.";
                return false;
            }

            validated = input;
            error = null;
            return true;
        }
    }

    /// <summary>Strict login name validation: ASCII letters, digits and underscore only.</summary>
    internal static class VendorRuntimeLoginName
    {
        private const int MinimumLength = 3;
        private const int MaximumLength = 128;
        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
            { "sa", "dbo", "guest", "sys", "public", "information_schema" };

        public static bool TryValidate(string input, out string? error)
        {
            if (string.IsNullOrWhiteSpace(input) ||
                input.Length < MinimumLength || input.Length > MaximumLength ||
                !char.IsAsciiLetter(input[0]) ||
                !input.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
            {
                error = "نام هویت Runtime باید با حرف انگلیسی شروع شود و فقط حرف، رقم و «_» داشته باشد (۳ تا ۱۲۸ نویسه).";
                return false;
            }

            if (ReservedNames.Contains(input))
            {
                error = "این نام برای SQL Server رزرو شده است؛ نام دیگری انتخاب کنید.";
                return false;
            }

            error = null;
            return true;
        }
    }

    /// <summary>
    /// Development provisioning only ever talks to a local SQL Server target. The operator's
    /// input is restricted server-side to loopback/local instance names plus an explicit
    /// configuration allow-list (FARANO_VENDOR_Development__Provisioning__AllowedSqlHosts),
    /// so the Development setup page can never become a SQL execution tool against arbitrary
    /// or internal-network hosts. Nothing about the owner's machine is hardcoded; a wildcard
    /// or partial match in the allow-list can never extend it silently.
    /// </summary>
    internal static class VendorSqlServerTarget
    {
        private static readonly HashSet<string> LocalNames = new(StringComparer.OrdinalIgnoreCase)
            { ".", "(local)", "(localdb)", "localhost", "127.0.0.1", "::1" };

        public static bool IsAllowed(
            string? server,
            IEnumerable<string> extraAllowedHosts,
            [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error,
            string? machineName = null)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(server) || server.Length > 256)
            {
                error = "نام سرور لازم است و حداکثر ۲۵۶ نویسه می‌تواند باشد.";
                return false;
            }

            var value = server.Trim();
            if (value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            {
                value = value["tcp:".Length..].Trim();
            }

            // Accepted shapes: host, host[,port], host\instance, host\instance[,port]. Anything
            // else (extra separators, empty instance, malformed port, second backslash) is
            // rejected before any connection string is built.
            string host;
            if (value.Contains('\\', StringComparison.Ordinal))
            {
                var instanceSeparator = value.IndexOf('\\');
                host = value[..instanceSeparator];
                var rest = value[(instanceSeparator + 1)..];
                if (rest.Length == 0 || rest.Contains('\\', StringComparison.Ordinal))
                {
                    error = "نام نمونهٔ SQL معتبر نیست.";
                    return false;
                }

                var portSeparator = rest.IndexOf(',');
                var instance = portSeparator < 0 ? rest : rest[..portSeparator];
                if (instance.Trim().Length == 0 || instance.Length > 16)
                {
                    error = "نام نمونهٔ SQL معتبر نیست.";
                    return false;
                }

                if (portSeparator >= 0 && !IsValidPort(rest[(portSeparator + 1)..]))
                {
                    error = "پورت SQL باید عددی بین ۱ تا ۶۵۵۳۵ باشد.";
                    return false;
                }
            }
            else
            {
                var portSeparator = value.IndexOf(',');
                if (portSeparator >= 0)
                {
                    host = value[..portSeparator];
                    if (!IsValidPort(value[(portSeparator + 1)..]))
                    {
                        error = "پورت SQL باید عددی بین ۱ تا ۶۵۵۳۵ باشد.";
                        return false;
                    }
                }
                else
                {
                    host = value;
                }
            }

            if (host.Length == 0 || host.Contains('*', StringComparison.Ordinal) ||
                host.Contains('?', StringComparison.Ordinal) ||
                host.Contains('%', StringComparison.Ordinal))
            {
                error = "نام سرور معتبر نیست.";
                return false;
            }

            // Bracket pairing is validated BEFORE trimming so an unbalanced form like "[::1"
            // cannot pass by having its unmatched bracket silently removed.
            if (host.StartsWith("[", StringComparison.Ordinal) != host.EndsWith("]", StringComparison.Ordinal))
            {
                error = "نام سرور معتبر نیست.";
                return false;
            }

            var hostName = host.Trim('[', ']').Trim();
            if (hostName.Length == 0)
            {
                error = "نام سرور معتبر نیست.";
                return false;
            }

            if (IPAddress.TryParse(hostName, out var address))
            {
                if (IPAddress.IsLoopback(address))
                {
                    return true;
                }

                // Non-loopback IP destinations are allowed only through the exact allow-list
                // (an owner-approved staging host); everything else is refused.
                if (extraAllowedHosts.Any(candidate =>
                        string.Equals(candidate.Trim(), hostName, StringComparison.OrdinalIgnoreCase) &&
                        !candidate.Contains('*', StringComparison.Ordinal)))
                {
                    return true;
                }

                error = NotAllowedMessage();
                return false;
            }

            if (LocalNames.Contains(hostName) ||
                string.Equals(hostName, machineName ?? Environment.MachineName, StringComparison.OrdinalIgnoreCase) ||
                extraAllowedHosts.Any(candidate =>
                    string.Equals(candidate.Trim(), hostName, StringComparison.OrdinalIgnoreCase) &&
                    candidate.Trim().Length > 0 &&
                    !candidate.Contains('*', StringComparison.Ordinal)))
            {
                return true;
            }

            error = NotAllowedMessage();
            return false;

            static string NotAllowedMessage() =>
                "مقصد انتخاب‌شده مجاز نیست؛ در Development فقط SQL Serverهای محلی مجازند. برای هدف تأییدشدهٔ دیگر، میزبان آن را در FARANO_VENDOR_Development__Provisioning__AllowedSqlHosts اضافه کنید.";
        }

        private static bool IsValidPort(string port) =>
            int.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out var portNumber) &&
            portNumber is >= 1 and <= 65535;
    }

    /// <summary>
    /// Ownership marker for a provisioned Vendor database: a database-level extended property
    /// written by the controlled create path (and backfilled for databases whose applied
    /// Vendor migration history already proves ownership). The property counts as proof only
    /// when its value matches the Vendor constant; a foreign or fabricated marker value fails
    /// closed. An existing database without valid proof is never adopted — an empty database
    /// could have been created for another purpose (including an empty Customer database).
    /// </summary>
    internal static class VendorOwnershipMarker
    {
        internal const string MarkerName = "FaranouVendorOwnership";
        internal const string MarkerValue = "Faranou Vendor database; created/adopted by the Development provisioning flow.";

        internal enum MarkerState
        {
            Absent,
            Valid,
            InvalidValue
        }

        public static async Task<MarkerState> ReadStateAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            // The marker name is a compile-time Vendor constant, bound as a parameter; the
            // SQL text itself is a literal at the call site (no dynamic SQL anywhere).
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CAST(COUNT(*) AS int) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @marker";
            command.Parameters.Add(new SqlParameter("@marker", System.Data.SqlDbType.NVarChar, 128) { Value = MarkerName });
            var count = await command.ExecuteScalarAsync(cancellationToken);
            if (count is not int present || present == 0)
            {
                return MarkerState.Absent;
            }

            // A row exists: SQL Server permits a NULL property value, and a NULL-valued
            // marker is not a valid proof — it is treated as a foreign/fabricated marker.
            await using var valueCommand = connection.CreateCommand();
            valueCommand.CommandText = "SELECT CAST(value AS nvarchar(256)) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @marker";
            valueCommand.Parameters.Add(new SqlParameter("@marker", System.Data.SqlDbType.NVarChar, 128) { Value = MarkerName });
            var value = await valueCommand.ExecuteScalarAsync(cancellationToken);
            if (value is null or DBNull)
            {
                return MarkerState.InvalidValue;
            }

            return string.Equals(value as string, MarkerValue, StringComparison.Ordinal)
                ? MarkerState.Valid
                : MarkerState.InvalidValue;
        }

        public static async Task<bool> ReadAsync(SqlConnection connection, CancellationToken cancellationToken) =>
            await ReadStateAsync(connection, cancellationToken) == MarkerState.Valid;

        /// <summary>
        /// Writes (or repairs) the marker with the Vendor value. Only called from paths that
        /// have already proven ownership: a database this run created, a database proven by
        /// its applied Vendor migration history, or a controlled recovery of an orphaned
        /// database recorded in the provisioning intent ledger.
        /// </summary>
        public static async Task WriteAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @marker)
                    EXEC sys.sp_updateextendedproperty @name = @marker, @value = @value;
                ELSE
                    EXEC sys.sp_addextendedproperty @name = @marker, @value = @value;
                """;
            command.Parameters.Add(new SqlParameter("@marker", System.Data.SqlDbType.NVarChar, 128) { Value = MarkerName });
            command.Parameters.Add(new SqlParameter("@value", System.Data.SqlDbType.NVarChar, 256) { Value = MarkerValue });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Pure ownership decision, unit-tested: migration runs only on a database this flow
        /// created (valid marker) or whose applied Vendor migration history proves it is a
        /// Vendor database. A fabricated marker value, unknown migration ids, an unmarked
        /// database with tables and an empty unmarked database all fail safely.
        /// </summary>
        public static (bool Allowed, string? Refusal) EvaluateOwnership(
            MarkerState markerState,
            bool historyTableExists,
            int appliedCount,
            int unknownCount,
            int userTableCount)
        {
            if (unknownCount > 0)
            {
                return (false, "دیتابیس شامل Migrationهای ناشناس برای این نسخهٔ برنامه است؛ احتمالاً نسخهٔ جدیدتری روی آن اجرا شده. Migration اجرا نمی‌شود.");
            }

            if (markerState == MarkerState.Valid)
            {
                return (true, null);
            }

            if (markerState == MarkerState.InvalidValue)
            {
                return (false, "نشانهٔ مالکیت روی دیتابیس با مقدار مورد انتظار Vendor مطابقت ندارد (نشانهٔ خارجی یا جعلی). برای جلوگیری از تصاحب دیتابیس ناشناس، Migration اجرا نمی‌شود؛ وضعیت را با مدیر سرور بررسی کنید.");
            }

            if (historyTableExists && appliedCount > 0)
            {
                // Pre-marker Vendor database: its own Faranou migration history proves
                // ownership; the marker is backfilled after the migration succeeds.
                return (true, null);
            }

            if (userTableCount > 0)
            {
                return (false, "این دیتابیس جدول‌هایی دارد اما نه نشانهٔ مالکیت Vendor و نه تاریخچهٔ Migration فارنو در آن نیست؛ برای حفاظت از دادهٔ ناشناس، Migration اجرا نمی‌شود.");
            }

            if (historyTableExists)
            {
                return (false, "تاریخچهٔ Migration دیتابیس خالی است و مالکیت Vendor روی آن قابل اثبات نیست؛ Migration اجرا نمی‌شود.");
            }

            return (false, "دیتابیس از قبل وجود دارد اما هیچ نشانه‌ای از مالکیت Vendor ندارد. یک دیتابیس خالی ممکن است برای هدف دیگری (مثلاً Customer) ساخته شده باشد؛ به‌طور خودکار تصاحب نمی‌شود. یک نام دیتابیس اختصاصی جدید انتخاب کنید یا مالکیت را با مدیر سرور تأیید کنید.");
        }
    }

    /// <summary>
    /// Controlled recovery for the crash window between CREATE DATABASE and the ownership
    /// marker write: before the create, the flow records a provisioning intent as an extended
    /// property on <c>master</c> (name + UTC time); after the marker is written, the intent is
    /// cleared. If the process dies in between, the next run finds an empty, unmarked database
    /// and recovery is allowed only when the intent matches the name exactly, is fresh, and
    /// the database's server-recorded <c>create_date</c> is not older than the intent — the
    /// binding that shows this provisioning flow (not some other service) created it. The
    /// database is never dropped; without the full evidence it stays anonymous, fail-closed.
    /// </summary>
    internal static class VendorProvisioningIntent
    {
        internal const string IntentName = "FaranouVendorProvisioningIntent";
        internal static readonly TimeSpan MaximumIntentAge = TimeSpan.FromHours(24);
        // SQL Server clock skew tolerance for the create-date binding (create_date must not
        // be older than the intent, minus this tolerance to stay robust across hosts).
        internal static readonly TimeSpan CreateDateSkewTolerance = TimeSpan.FromMinutes(1);

        internal enum IntentState
        {
            Absent,
            Matching,
            Expired,
            ForOtherDatabase,
            NotMatching
        }

        public static string FormatValue(string databaseName, DateTimeOffset issuedUtc) =>
            FormattableString.Invariant($"VendorDb={databaseName};IssuedUtc={issuedUtc:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}");

        public static bool TryParse(string? value, out string? databaseName, out DateTimeOffset? issuedUtc)
        {
            databaseName = null;
            issuedUtc = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            // The value is written by this flow only; parse strictly, without eval-style
            // trust. Empty entries are NOT removed: exactly two `;`-separated fields in the
            // exact emitted order are accepted, so a record with extra delimiters cannot pass.
            var parts = value.Split(';', StringSplitOptions.None);
            if (parts.Length != 2)
            {
                return false;
            }

            if (!parts[0].StartsWith("VendorDb=", StringComparison.Ordinal) ||
                !parts[1].StartsWith("IssuedUtc=", StringComparison.Ordinal))
            {
                return false;
            }

            databaseName = parts[0]["VendorDb=".Length..];
            if (DateTimeOffset.TryParseExact(
                    parts[1]["IssuedUtc=".Length..],
                    "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                issuedUtc = parsed;
                return true;
            }

            return false;
        }

        public static IntentState Classify(string? storedValue, string databaseName, DateTimeOffset nowUtc)
        {
            if (string.IsNullOrEmpty(storedValue))
            {
                return IntentState.Absent;
            }

            if (!TryParse(storedValue, out var intentName, out var issuedUtc) ||
                intentName is null || issuedUtc is null)
            {
                return IntentState.NotMatching;
            }

            if (!string.Equals(intentName, databaseName, StringComparison.Ordinal))
            {
                // Case-sensitive comparison on purpose: on a case-sensitive server collation
                // `VendorDb` and `vendordb` are different databases, and matching loosely
                // could authorize recovery of a database the intent never named.
                return IntentState.ForOtherDatabase;
            }

            var age = nowUtc - issuedUtc.Value;
            return age >= TimeSpan.Zero && age <= MaximumIntentAge ? IntentState.Matching : IntentState.Expired;
        }

        /// <summary>
        /// Reads the server-recorded creation time of a database (UTC) from
        /// <c>sys.databases</c>; used as the recovery evidence binding (see EvaluateRecovery).
        /// </summary>
        public static async Task<DateTimeOffset?> ReadDatabaseCreatedUtcAsync(
            SqlConnection masterConnection,
            string databaseName,
            CancellationToken cancellationToken)
        {
            await using var command = masterConnection.CreateCommand();
            command.CommandText = "SELECT create_date FROM sys.databases WHERE name = @name";
            command.Parameters.Add(new SqlParameter("@name", System.Data.SqlDbType.NVarChar, 128) { Value = databaseName });
            var created = await command.ExecuteScalarAsync(cancellationToken);
            if (created is not DateTime createdUtc)
            {
                return null;
            }

            return new DateTimeOffset(createdUtc, TimeSpan.Zero);
        }

        public static async Task<string?> ReadAsync(SqlConnection masterConnection, CancellationToken cancellationToken)
        {
            // The intent name is a compile-time Vendor constant, bound as a parameter.
            await using var command = masterConnection.CreateCommand();
            command.CommandText = "SELECT CAST(value AS nvarchar(512)) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @intent";
            command.Parameters.Add(new SqlParameter("@intent", System.Data.SqlDbType.NVarChar, 128) { Value = IntentName });
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value as string;
        }

        /// <summary>
        /// Writes the intent evidence INSIDE the target database as well. Together with the
        /// master-side record this is the durable proof that THIS flow created the database:
        /// another service creating a same-name database after our master-side intent would
        /// never carry the in-database record, so recovery stays closed for it.
        /// </summary>
        public static async Task WriteInDatabaseAsync(SqlConnection targetConnection, string databaseName, DateTimeOffset issuedUtc, CancellationToken cancellationToken)
        {
            await using var command = targetConnection.CreateCommand();
            command.CommandText = """
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @intent)
                    EXEC sys.sp_updateextendedproperty @name = @intent, @value = @value;
                ELSE
                    EXEC sys.sp_addextendedproperty @name = @intent, @value = @value;
                """;
            command.Parameters.Add(new SqlParameter("@intent", System.Data.SqlDbType.NVarChar, 128) { Value = IntentName });
            command.Parameters.Add(new SqlParameter("@value", System.Data.SqlDbType.NVarChar, 512) { Value = FormatValue(databaseName, issuedUtc) });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public static async Task<string?> ReadInDatabaseAsync(SqlConnection targetConnection, CancellationToken cancellationToken)
        {
            await using var command = targetConnection.CreateCommand();
            command.CommandText = "SELECT CAST(value AS nvarchar(512)) FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @intent";
            command.Parameters.Add(new SqlParameter("@intent", System.Data.SqlDbType.NVarChar, 128) { Value = IntentName });
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value as string;
        }

        public static async Task ClearInDatabaseAsync(SqlConnection targetConnection, CancellationToken cancellationToken)
        {
            await using var command = targetConnection.CreateCommand();
            command.CommandText = """
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @intent)
                    EXEC sys.sp_dropextendedproperty @name = @intent;
                """;
            command.Parameters.Add(new SqlParameter("@intent", System.Data.SqlDbType.NVarChar, 128) { Value = IntentName });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public static async Task WriteAsync(SqlConnection masterConnection, string databaseName, DateTimeOffset issuedUtc, CancellationToken cancellationToken)
        {
            await using var command = masterConnection.CreateCommand();
            command.CommandText = """
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @intent)
                    EXEC sys.sp_updateextendedproperty @name = @intent, @value = @value;
                ELSE
                    EXEC sys.sp_addextendedproperty @name = @intent, @value = @value;
                """;
            command.Parameters.Add(new SqlParameter("@intent", System.Data.SqlDbType.NVarChar, 128) { Value = IntentName });
            command.Parameters.Add(new SqlParameter("@value", System.Data.SqlDbType.NVarChar, 512) { Value = FormatValue(databaseName, issuedUtc) });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public static async Task ClearAsync(SqlConnection masterConnection, CancellationToken cancellationToken)
        {
            await using var command = masterConnection.CreateCommand();
            command.CommandText = """
                IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE class = 0 AND major_id = 0 AND minor_id = 0 AND name = @intent)
                    EXEC sys.sp_dropextendedproperty @name = @intent;
                """;
            command.Parameters.Add(new SqlParameter("@intent", System.Data.SqlDbType.NVarChar, 128) { Value = IntentName });
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// Pure recovery decision: an empty, unmarked database may only be recovered when a
        /// fresh intent record matches its name exactly AND the database's server-recorded
        /// creation time is not older than the intent (minus the skew tolerance) — the
        /// binding that ties the database to this flow's own create attempt. A database that
        /// existed before the intent was recorded, or any other mismatch, fails closed; the
        /// database is never dropped here.
        /// </summary>
        public static (bool Allowed, string? Refusal) EvaluateRecovery(
            IntentState intentState,
            DateTimeOffset? databaseCreatedUtc,
            DateTimeOffset? intentIssuedUtc,
            bool historyTableExists,
            int userTableCount,
            bool inDatabaseIntentPresent = true)
        {
            if (intentState != IntentState.Matching)
            {
                return (false, null);
            }

            if (!inDatabaseIntentPresent)
            {
                // The in-database record is the durable proof that THIS flow created the
                // database; without it, timing and emptiness alone prove nothing.
                return (false, "رکورد قصد داخل دیتابیس یافت نشد؛ این دیتابیس با این جریان ساخته نشده است و Recovery انجام نمی‌شود.");
            }

            if (historyTableExists || userTableCount > 0)
            {
                return (false, "دیتابیس هم‌نام رکورد راه‌اندازی پیدا شد اما خالی نیست؛ Recovery خودکار انجام نمی‌شود و وضعیت با مدیر سرور بررسی شود.");
            }

            if (databaseCreatedUtc is null || intentIssuedUtc is null ||
                databaseCreatedUtc.Value < intentIssuedUtc.Value - CreateDateSkewTolerance)
            {
                // The database existed before the intent was recorded, or its creation time
                // cannot be proven: the intent alone is not ownership proof, so stay closed.
                return (false, "زمان ایجاد ثبت‌شدهٔ دیتابیس با رکورد راه‌اندازی هم‌خوان نیست؛ این دیتابیس با این جریان ساخته نشده و Recovery انجام نمی‌شود.");
            }

            return (true, null);
        }

        internal static string RecoveryMessage(string databaseName) =>
            $"دیتابیس «{databaseName}» خالی است و نشانهٔ مالکیت ندارد؛ با این حال رکورد راه‌اندازی این جریان روی آن ثبت شده است (احتمالاً اجرای قبلی بین ساخت دیتابیس و ثبت نشانه متوقف شده). دیتابیس حذف نمی‌شود و مالکیت آن به‌صورت کنترل‌شده بازسازی می‌شود.";
    }
}
