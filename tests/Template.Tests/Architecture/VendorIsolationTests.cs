using System.Xml.Linq;
using Xunit;

namespace Template.Tests.Architecture;

/// <summary>
/// Regression guards for the independent Vendor control-plane boundary (D-041 / D-047).
/// These tests inspect project metadata and never build or execute Vendor code.
/// </summary>
public sealed class VendorIsolationTests
{
    [Fact]
    public void ServerProjectReferenceGraph_DoesNotReachVendorProjects()
    {
        var repositoryRoot = FindRepositoryRoot();
        var serverProject = Path.Combine(repositoryRoot, "Server", "Server.csproj");

        var violations = FindServerVendorBoundaryViolations(repositoryRoot, serverProject);

        Assert.True(
            violations.Count == 0,
            "Server must not reference Vendor projects directly or transitively. " +
            "Project graph violations:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void LicensingContracts_HasNoProjectReferencesOrLinkedSourcesOutsideItsDirectory()
    {
        var repositoryRoot = FindRepositoryRoot();
        var contractsProject = Path.Combine(repositoryRoot, "Licensing.Contracts", "Licensing.Contracts.csproj");
        var projectDirectory = Path.GetDirectoryName(contractsProject)!;
        var document = XDocument.Load(contractsProject);
        var violations = new List<string>();

        foreach (var projectReference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
        {
            violations.Add($"Licensing.Contracts must not contain ProjectReference: {projectReference}");
        }

        foreach (var packageReference in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
        {
            var packageName = (string?)packageReference.Attribute("Include");
            if (packageName is not null &&
                (packageName.StartsWith("Microsoft.AspNetCore.", StringComparison.OrdinalIgnoreCase) ||
                 packageName.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.OrdinalIgnoreCase) ||
                 packageName.StartsWith("Faranou.Vendor.", StringComparison.OrdinalIgnoreCase)))
            {
                violations.Add($"Licensing.Contracts must not depend on web, EF Core, or Vendor packages: {packageName}");
            }
        }

        foreach (var linkedItem in document.Descendants().Where(element =>
                     (element.Name.LocalName is "Compile" or "Content") &&
                     (element.Attribute("Link") is not null || element.Elements().Any(child => child.Name.LocalName == "Link")))
        {
            var include = (string?)linkedItem.Attribute("Include");
            if (string.IsNullOrWhiteSpace(include) || include.Contains("$(", StringComparison.Ordinal) ||
                include.Contains('*') || include.Contains('?'))
            {
                violations.Add($"Cannot prove linked source stays inside Licensing.Contracts: {linkedItem}");
                continue;
            }

            var sourcePath = Path.GetFullPath(Path.Combine(projectDirectory, NormalizeProjectPath(include)));
            if (!IsWithinDirectory(projectDirectory, sourcePath))
            {
                violations.Add($"Licensing.Contracts links a source outside its directory: {include}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Licensing.Contracts must remain a dependency-free contract project with no linked source from other areas. " +
            string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void DependencyGuard_DetectsTransitiveVendorReferenceInSyntheticProjectGraph()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "faranou-vendor-guard-" + Guid.NewGuid().ToString("N"));
        var serverDirectory = Directory.CreateDirectory(Path.Combine(temporaryRoot, "Server")).FullName;
        var sharedDirectory = Directory.CreateDirectory(Path.Combine(temporaryRoot, "Shared")).FullName;
        var vendorDirectory = Directory.CreateDirectory(Path.Combine(temporaryRoot, "Vendor", "Host")).FullName;

        try
        {
            WriteProject(Path.Combine(serverDirectory, "Server.csproj"), "../Shared/Shared.csproj", "Contoso.Vendor.Security");
            WriteProject(Path.Combine(sharedDirectory, "Shared.csproj"), "../Vendor/Host/Host.csproj");
            WriteProject(Path.Combine(vendorDirectory, "Host.csproj"));

            var violations = FindServerVendorBoundaryViolations(
                temporaryRoot,
                Path.Combine(serverDirectory, "Server.csproj"));

            Assert.True(
                violations.Any(violation => violation.Contains("Forbidden Vendor project reference", StringComparison.Ordinal)),
                "The guard must detect a Vendor project reached through a transitive ProjectReference.");
            Assert.True(
                violations.Any(violation => violation.Contains("Forbidden Vendor-specific PackageReference", StringComparison.Ordinal)),
                "The guard must reject Vendor-specific NuGet dependencies in the Server project graph.");
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static IReadOnlyList<string> FindServerVendorBoundaryViolations(string repositoryRoot, string startingProject)
    {
        var vendorDirectory = Path.GetFullPath(Path.Combine(repositoryRoot, "Vendor"));
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var violations = new List<string>();

        Visit(startingProject);
        return violations;

        void Visit(string projectPath)
        {
            projectPath = Path.GetFullPath(projectPath);
            if (!visited.Add(projectPath))
            {
                return;
            }

            if (!File.Exists(projectPath))
            {
                violations.Add($"ProjectReference target does not exist: {projectPath}");
                return;
            }

            var document = XDocument.Load(projectPath);
            foreach (var packageReference in document.Descendants().Where(element => element.Name.LocalName == "PackageReference"))
            {
                var packageName = (string?)packageReference.Attribute("Include");
                if (packageName is not null && packageName.Contains("Vendor", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add($"Forbidden Vendor-specific PackageReference in {projectPath}: {packageName}");
                }
            }

            foreach (var reference in document.Descendants().Where(element => element.Name.LocalName == "ProjectReference"))
            {
                var include = (string?)reference.Attribute("Include");
                if (string.IsNullOrWhiteSpace(include) || include.Contains("$(", StringComparison.Ordinal) ||
                    include.Contains('*') || include.Contains('?'))
                {
                    violations.Add($"Cannot statically verify ProjectReference in {projectPath}: {reference}");
                    continue;
                }

                var referencedProject = Path.GetFullPath(
                    Path.Combine(Path.GetDirectoryName(projectPath)!, NormalizeProjectPath(include)));

                if (IsWithinDirectory(vendorDirectory, referencedProject))
                {
                    violations.Add($"Forbidden Vendor project reference: {projectPath} -> {referencedProject}");
                    continue;
                }

                Visit(referencedProject);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startingDirectory in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? current = new DirectoryInfo(startingDirectory);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "Template.sln")) &&
                    File.Exists(Path.Combine(current.FullName, "Vendor.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate repository root containing both Template.sln and Vendor.sln.");
    }

    private static bool IsWithinDirectory(string directory, string candidatePath)
    {
        var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullCandidate = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return fullCandidate.Equals(fullDirectory, comparison) ||
               fullCandidate.StartsWith(fullDirectory + Path.DirectorySeparatorChar, comparison) ||
               fullCandidate.StartsWith(fullDirectory + Path.AltDirectorySeparatorChar, comparison);
    }

    private static string NormalizeProjectPath(string path) =>
        path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static void WriteProject(string path, string? projectReference = null, string? packageReference = null)
    {
        var project = new XElement("Project");
        var itemGroup = new XElement("ItemGroup");
        if (projectReference is not null)
        {
            itemGroup.Add(new XElement("ProjectReference", new XAttribute("Include", projectReference)));
        }

        if (packageReference is not null)
        {
            itemGroup.Add(new XElement("PackageReference", new XAttribute("Include", packageReference)));
        }

        if (itemGroup.HasElements)
        {
            project.Add(itemGroup);
        }

        new XDocument(project).Save(path);
    }
}