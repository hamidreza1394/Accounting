using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Template.Tests;

/// <summary>
/// G1 (D-041): automated architecture dependency guards for the customer application.
/// Ensures the Server project graph never reaches Vendor/** and that Licensing.Contracts
/// (when present) stays free of ProjectReference / external Compile|Content links.
/// Negative scenarios use a temporary fixture tree so the real solution is never modified.
/// </summary>
public sealed class ArchitectureDependencyGuardTests
{
	private static readonly string RepoRoot = FindRepoRoot();

	[Fact]
	public void Server_project_graph_must_not_reference_Vendor()
	{
		var serverCsproj = Path.Combine(RepoRoot, "Server", "Server.csproj");
		Assert.True(File.Exists(serverCsproj), "Server/Server.csproj must exist");

		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var queue = new Queue<string>();
		queue.Enqueue(Path.GetFullPath(serverCsproj));

		while (queue.Count > 0)
		{
			var path = queue.Dequeue();
			if (!visited.Add(path))
			{
				continue;
			}

			var relative = Path.GetRelativePath(RepoRoot, path).Replace('\\', '/');
			Assert.False(
				relative.Contains("/Vendor/", StringComparison.OrdinalIgnoreCase)
				|| relative.StartsWith("Vendor/", StringComparison.OrdinalIgnoreCase),
				$"Customer graph reached a Vendor project: {relative}");

			foreach (var reference in ReadProjectReferences(path))
			{
				var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, reference));
				if (File.Exists(resolved))
				{
					queue.Enqueue(resolved);
				}
			}
		}
	}

	[Fact]
	public void Licensing_Contracts_when_present_must_have_no_project_or_external_links()
	{
		var contracts = Path.Combine(RepoRoot, "Licensing.Contracts", "Licensing.Contracts.csproj");
		if (!File.Exists(contracts))
		{
			// Not created yet (L1 after guards). Guard is ready for when it appears.
			return;
		}

		var refs = ReadProjectReferences(contracts).ToList();
		Assert.True(refs.Count == 0, "Licensing.Contracts must not have ProjectReference entries: " + string.Join(", ", refs));

		var links = ReadExternalCompileOrContentLinks(contracts, Path.GetDirectoryName(contracts)!).ToList();
		Assert.True(links.Count == 0, "Licensing.Contracts must not link Compile/Content outside its folder: " + string.Join(", ", links));
	}

	[Fact]
	public void Negative_fixture_detects_forbidden_Vendor_ProjectReference()
	{
		var fixtureRoot = Path.Combine(Path.GetTempPath(), "faranou-g1-" + Guid.NewGuid().ToString("N"));
		try
		{
			var serverDir = Path.Combine(fixtureRoot, "Server");
			var vendorDir = Path.Combine(fixtureRoot, "Vendor", "Host");
			Directory.CreateDirectory(serverDir);
			Directory.CreateDirectory(vendorDir);

			File.WriteAllText(Path.Combine(vendorDir, "Vendor.Host.csproj"),
				"""
				<Project Sdk="Microsoft.NET.Sdk">
				  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
				</Project>
				""".Trim());

			File.WriteAllText(Path.Combine(serverDir, "Server.csproj"),
				"""
				<Project Sdk="Microsoft.NET.Sdk.Web">
				  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
				  <ItemGroup>
				    <ProjectReference Include="..\\Vendor\\Host\\Vendor.Host.csproj" />
				  </ItemGroup>
				</Project>
				""".Trim());

			var violations = CollectVendorReachableFrom(Path.Combine(serverDir, "Server.csproj"), fixtureRoot);
			Assert.True(violations.Count > 0, "Negative fixture must detect the Vendor ProjectReference");
			Assert.Contains(violations, v => v.Contains("Vendor", StringComparison.OrdinalIgnoreCase));
		}
		finally
		{
			try { Directory.Delete(fixtureRoot, recursive: true); } catch { /* best-effort cleanup */ }
		}
	}

	[Fact]
	public void Server_csproj_must_not_list_Vendor_package_ids()
	{
		var serverCsproj = Path.Combine(RepoRoot, "Server", "Server.csproj");
		var text = File.ReadAllText(serverCsproj);
		// Narrow policy: PackageReference Include values that start with Vendor. are forbidden.
		var matches = Regex.Matches(text, "<PackageReference\\s+Include=\"(Vendor\\.[^\"]+)\"", RegexOptions.IgnoreCase);
		Assert.True(matches.Count == 0, "Server must not reference Vendor.* packages: " +
			string.Join(", ", matches.Select(m => m.Groups[1].Value)));
	}

	private static List<string> CollectVendorReachableFrom(string startCsproj, string root)
	{
		var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var queue = new Queue<string>();
		var violations = new List<string>();
		queue.Enqueue(Path.GetFullPath(startCsproj));

		while (queue.Count > 0)
		{
			var path = queue.Dequeue();
			if (!visited.Add(path))
			{
				continue;
			}

			var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
			if (relative.Contains("/Vendor/", StringComparison.OrdinalIgnoreCase)
			    || relative.StartsWith("Vendor/", StringComparison.OrdinalIgnoreCase))
			{
				violations.Add(relative);
			}

			foreach (var reference in ReadProjectReferences(path))
			{
				var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, reference));
				if (File.Exists(resolved))
				{
					queue.Enqueue(resolved);
				}
			}
		}

		return violations;
	}

	private static IEnumerable<string> ReadProjectReferences(string csprojPath)
	{
		var doc = XDocument.Load(csprojPath);
		return doc.Descendants()
			.Where(e => e.Name.LocalName == "ProjectReference")
			.Select(e => (string?)e.Attribute("Include"))
			.Where(v => !string.IsNullOrWhiteSpace(v))
			.Select(v => v!);
	}

	private static IEnumerable<string> ReadExternalCompileOrContentLinks(string csprojPath, string projectDir)
	{
		var doc = XDocument.Load(csprojPath);
		var projectFull = Path.GetFullPath(projectDir);
		foreach (var el in doc.Descendants().Where(e => e.Name.LocalName is "Compile" or "Content" or "EmbeddedResource" or "None"))
		{
			var link = (string?)el.Attribute("Link");
			var include = (string?)el.Attribute("Include");
			var candidate = link ?? include;
			if (string.IsNullOrWhiteSpace(candidate))
			{
				continue;
			}

			// Absolute or parent-path includes that escape the project folder are forbidden.
			var combined = Path.GetFullPath(Path.Combine(projectDir, candidate));
			if (!combined.StartsWith(projectFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
			    && !string.Equals(combined, projectFull, StringComparison.OrdinalIgnoreCase))
			{
				yield return candidate;
			}
		}
	}

	private static string FindRepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null)
		{
			if (File.Exists(Path.Combine(dir.FullName, "Template.sln"))
			    && File.Exists(Path.Combine(dir.FullName, "Server", "Server.csproj")))
			{
				return dir.FullName;
			}

			dir = dir.Parent;
		}

		throw new InvalidOperationException("Could not locate repository root (Template.sln + Server/Server.csproj).");
	}
}
