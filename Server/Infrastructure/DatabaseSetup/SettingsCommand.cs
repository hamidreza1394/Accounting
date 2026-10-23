namespace Infrastructure.DatabaseSetup;

/// <summary>
/// <c>dotnet Server.dll --settings status</c> and
/// <c>--settings migrate [--replace-current|--keep-current]</c>: operator commands run before the
/// web host starts. Output never contains the connection string or any key material.
/// Exit codes: 0 = done / usable, 1 = failure or unreadable settings, 2 = a decision or setup is
/// still needed (conflict, not configured, wrong usage).
/// </summary>
public static class SettingsCommand
{
    public const string Argument = "--settings";

    public static bool IsSettingsCommand(IEnumerable<string> args) =>
        args.Any(arg => string.Equals(arg, Argument, StringComparison.OrdinalIgnoreCase));

    public static int Run(
        string[] args,
        DatabaseConnectionSettingsStore store,
        DatabaseSettingsLocation location,
        TextWriter output,
        TextWriter error)
    {
        var index = Array.FindIndex(args, arg => string.Equals(arg, Argument, StringComparison.OrdinalIgnoreCase));
        var verb = index + 1 < args.Length ? args[index + 1].ToLowerInvariant() : string.Empty;
        var flags = args.Skip(index + 2).Select(flag => flag.ToLowerInvariant()).ToArray();

        switch (verb)
        {
            case "status" when flags.Length == 0:
                return Status(store, location, output);

            case "migrate" when flags.All(flag => flag is "--replace-current" or "--keep-current"):
                var replace = flags.Contains("--replace-current");
                var keep = flags.Contains("--keep-current");
                if (replace && keep)
                {
                    error.WriteLine("Use either --replace-current or --keep-current, not both.");
                    return 2;
                }

                return Migrate(store, location, replace, keep, output, error);

            default:
                error.WriteLine("Usage: --settings status | --settings migrate [--replace-current | --keep-current]");
                return 2;
        }
    }

    public static int RunOnConsole(
        string[] args,
        DatabaseConnectionSettingsStore store,
        DatabaseSettingsLocation location) =>
        Run(args, store, location, Console.Out, Console.Error);

    private static int Status(DatabaseConnectionSettingsStore store, DatabaseSettingsLocation location, TextWriter output)
    {
        var result = store.Load(readOnly: true);
        WriteReport(output, store, location, result);
        return ExitCodeOf(result);
    }

    private static int Migrate(
        DatabaseConnectionSettingsStore store,
        DatabaseSettingsLocation location,
        bool replace,
        bool keep,
        TextWriter output,
        TextWriter error)
    {
        DatabaseSettingsLoadResult result;
        try
        {
            result = store.MigrateFromLegacy(replace, keep);
        }
        catch (DatabaseSettingsStoreException exception)
        {
            error.WriteLine($"Migration failed: {exception.Code}. The previous settings were kept; the legacy file was not changed.");
            return 1;
        }

        WriteReport(output, store, location, result);
        if (result.Status == DatabaseSettingsStatus.Conflict)
        {
            output.WriteLine("The legacy and the current connection differ. Decide explicitly:");
            output.WriteLine("  --settings migrate --replace-current   use the legacy connection (current one is backed up)");
            output.WriteLine("  --settings migrate --keep-current      keep the current connection and stop warning");
            return 2;
        }

        return ExitCodeOf(result);
    }

    private static int ExitCodeOf(DatabaseSettingsLoadResult result) => result.Status switch
    {
        DatabaseSettingsStatus.Loaded or DatabaseSettingsStatus.LoadedFromLegacy or DatabaseSettingsStatus.MigratedFromLegacy => 0,
        DatabaseSettingsStatus.NotConfigured or DatabaseSettingsStatus.Conflict => 2,
        _ => 1,
    };

    private static void WriteReport(
        TextWriter output,
        DatabaseConnectionSettingsStore store,
        DatabaseSettingsLocation location,
        DatabaseSettingsLoadResult result)
    {
        output.WriteLine($"Status:            {result.Status}");
        output.WriteLine($"Settings folder:   {store.SettingsDirectory} (source: {location.Source})");
        output.WriteLine($"Settings file:     {(File.Exists(store.SettingsFilePath) ? "present" : "missing")}");
        output.WriteLine($"Legacy folder:     {location.LegacyRoot ?? "(unknown)"}");
        output.WriteLine($"Legacy file:       {(store.LegacySettingsFilePath is not null && File.Exists(store.LegacySettingsFilePath) ? "present (never modified)" : "missing")}");
        output.WriteLine($"Migration marker:  {(File.Exists(store.MarkerFilePath) ? "present" : "missing")}");
        output.WriteLine($"Warnings:          {(result.WarningCodes.Count == 0 ? "none" : string.Join(", ", result.WarningCodes))}");
        output.WriteLine("The connection string is never printed.");
    }
}
