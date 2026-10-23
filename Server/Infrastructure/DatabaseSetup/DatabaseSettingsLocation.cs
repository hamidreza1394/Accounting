using Microsoft.Extensions.Configuration;

namespace Infrastructure.DatabaseSetup;

/// <summary>Where the protected connection settings live (and where the legacy ones were).</summary>
/// <param name="Root">Folder of the current settings (never inside the application folder).</param>
/// <param name="Source"><see cref="ConfigurationSource" /> or <see cref="DefaultSource" />.</param>
/// <param name="LegacyRoot">Read-only folder of the previous product version; null when unknown.</param>
public sealed record DatabaseSettingsLocation(string Root, string Source, string? LegacyRoot)
{
    public const string ConfigurationSource = "Configuration";
    public const string DefaultSource = "LocalApplicationData";

    /// <summary>
    /// Order: configuration key <c>Farano:Settings:Directory</c> (also the environment variable
    /// <c>Farano__Settings__Directory</c>); otherwise LocalApplicationData/Farano/Settings.
    /// Nothing is created here.
    /// </summary>
    /// <param name="localApplicationData">Null reads the real folder; tests pass a temp folder.</param>
    public static DatabaseSettingsLocation Resolve(
        IConfiguration? configuration,
        string? localApplicationData = null,
        string? applicationBaseDirectory = null,
        string? contentRootPath = null,
        string? webRootPath = null)
    {
        localApplicationData ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        applicationBaseDirectory ??= AppContext.BaseDirectory;

        var legacyRoot = string.IsNullOrWhiteSpace(localApplicationData)
            ? null
            : Path.Combine(localApplicationData, LegacySettingsNames.FolderName, LegacySettingsNames.DatabaseFolderName);

        var configured = configuration?[FaranoSettingsNames.SettingsDirectoryKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var custom = configured.Trim();
            if (!Path.IsPathFullyQualified(custom))
            {
                throw new InvalidOperationException(
                    $"مقدار «{FaranoSettingsNames.SettingsDirectoryKey}» باید مسیر کامل (absolute) باشد. " +
                    $"The value of '{FaranoSettingsNames.SettingsDirectoryKey}' must be a fully qualified path.");
            }

            var full = Path.GetFullPath(custom);
            foreach (var forbidden in new[] { applicationBaseDirectory, contentRootPath, webRootPath })
            {
                if (!string.IsNullOrWhiteSpace(forbidden) && IsInside(full, forbidden))
                {
                    throw new InvalidOperationException(
                        $"مقدار «{FaranoSettingsNames.SettingsDirectoryKey}» نباید داخل پوشه برنامه باشد؛ با به‌روزرسانی برنامه پاک می‌شود. " +
                        $"'{FaranoSettingsNames.SettingsDirectoryKey}' must not point inside the application folder.");
                }
            }

            return new DatabaseSettingsLocation(full, ConfigurationSource, legacyRoot);
        }

        if (string.IsNullOrWhiteSpace(localApplicationData))
        {
            throw new InvalidOperationException(
                $"پوشه LocalApplicationData در دسترس نیست؛ مسیر قابل‌نوشتن را با «{FaranoSettingsNames.SettingsDirectoryKey}» " +
                $"(متغیر محیطی Farano__Settings__Directory) تعیین کنید. " +
                $"LocalApplicationData is unavailable; set '{FaranoSettingsNames.SettingsDirectoryKey}' (environment variable Farano__Settings__Directory) to a writable folder.");
        }

        return new DatabaseSettingsLocation(
            Path.Combine(localApplicationData, FaranoSettingsNames.FolderName, "Settings"),
            DefaultSource,
            legacyRoot);
    }

    private static bool IsInside(string candidate, string container)
    {
        // Compare real locations: a symbolic link or junction must not smuggle the folder into the app folder.
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var folder = Path.TrimEndingDirectorySeparator(ResolveRealPath(container));
        var path = Path.TrimEndingDirectorySeparator(ResolveRealPath(candidate));
        return path.Equals(folder, comparison)
            || path.StartsWith(folder + Path.DirectorySeparatorChar, comparison);
    }

    /// <summary>
    /// Follows links of every existing component of the path (the tail that does not exist yet is
    /// appended as is), so the result is where the folder would really be created.
    /// </summary>
    public static string ResolveRealPath(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        var current = root;
        var parts = full[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var exists = true;

        foreach (var part in parts)
        {
            current = Path.Combine(current, part);
            if (!exists)
            {
                continue;
            }

            try
            {
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if (!info.Exists)
                {
                    exists = false;
                    continue;
                }

                if (info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                {
                    current = target.FullName;
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Cannot look further (permissions): compare what is known.
                exists = false;
            }
        }

        return current;
    }
}
