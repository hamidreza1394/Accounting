using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Template.Tests;

/// <summary>
/// PR-E1b: the administrator file manager must never read, list, create, upload to or delete anything
/// outside its <c>wwwroot</c> root, whatever <c>path</c>, item name, directory name or file name is posted.
/// The tests drive the real page model against a throw-away content root; the secret file lives next to
/// <c>wwwroot</c>, outside the allowed boundary.
/// </summary>
public sealed class FileManagerPathBoundaryTests : IDisposable
{
	private readonly string contentRoot;
	private readonly string wwwroot;
	private readonly string secretDirectory;
	private readonly string secretFile;

	public FileManagerPathBoundaryTests()
	{
		contentRoot = Path.Combine(Path.GetTempPath(), "fm-" + Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar;
		wwwroot = Path.Combine(contentRoot, "wwwroot");
		secretDirectory = Path.Combine(contentRoot, "secret");
		secretFile = Path.Combine(secretDirectory, "key.txt");

		Directory.CreateDirectory(Path.Combine(wwwroot, "Images"));
		File.WriteAllText(Path.Combine(wwwroot, "Images", "logo.png"), "png");
		File.WriteAllText(Path.Combine(wwwroot, "root.png"), "png");
		Directory.CreateDirectory(secretDirectory);
		File.WriteAllText(secretFile, "top-secret");
	}

	public void Dispose()
	{
		try { Directory.Delete(contentRoot, recursive: true); } catch (IOException) { }
	}

	[Theory]
	[InlineData("/../secret/")]
	[InlineData("../secret")]
	[InlineData("..\\secret")]
	[InlineData("/Images/../../secret/")]
	public void ListingOutsideTheRootIsRefused(string path)
	{
		var page = CreatePage();

		page.OnGet(path);

		Assert.Equal("/", page.CurrentPath);
		Assert.DoesNotContain(page.Files, file => file.Name == "key.txt");
		Assert.DoesNotContain(page.Directories, directory => directory.Name == "secret");
	}

	[Fact]
	public void ListingASubdirectoryInsideTheRootStillWorks()
	{
		var page = CreatePage();

		page.OnGet("/Images");

		Assert.Equal("/Images/", page.CurrentPath);
		Assert.Contains(page.Files, file => file.Name == "logo.png");
	}

	[Theory]
	[InlineData("/", "../secret/key.txt")]
	[InlineData("/", "..\\secret\\key.txt")]
	[InlineData("/Images/", "../../secret/key.txt")]
	[InlineData("/Images/", "../../secret")]
	public void DeletingOutsideTheRootIsRefused(string path, string item)
	{
		var page = CreatePage(deleteEnabled: true);

		page.OnPostDeleteItems(path, new List<string> { item });

		Assert.True(File.Exists(secretFile));
		Assert.True(Directory.Exists(secretDirectory));
	}

	[Fact]
	public void DeletingAnItemInsideTheRootStillWorks()
	{
		var page = CreatePage(deleteEnabled: true);

		page.OnPostDeleteItems("/Images/", new List<string> { "logo.png" });

		Assert.False(File.Exists(Path.Combine(wwwroot, "Images", "logo.png")));
		Assert.True(File.Exists(secretFile));
	}

	[Theory]
	[InlineData("../evil")]
	[InlineData("..\\evil")]
	[InlineData("../../evil")]
	public void CreatingADirectoryOutsideTheRootIsRefused(string directoryName)
	{
		var page = CreatePage(createDirectoryEnabled: true);

		page.OnPostCreateDirectory("/Images/", directoryName);

		Assert.False(Directory.Exists(Path.Combine(contentRoot, "evil")));
		Assert.False(Directory.Exists(Path.Combine(wwwroot, "evil")));
		Assert.False(Directory.Exists(Path.Combine(contentRoot, "..", "evil")));
	}

	[Fact]
	public void CreatingADirectoryInsideTheRootStillWorks()
	{
		var page = CreatePage(createDirectoryEnabled: true);

		page.OnPostCreateDirectory("/Images/", "NewFolder");

		Assert.True(Directory.Exists(Path.Combine(wwwroot, "Images", "NewFolder")));
	}

	[Theory]
	[InlineData("../evil.png")]
	[InlineData("..\\evil.png")]
	[InlineData("../../evil.png")]
	public async Task UploadingOutsideTheRootIsRefused(string fileName)
	{
		var page = CreatePage(uploadEnabled: true);
		await using var content = new MemoryStream(new byte[] { 1, 2, 3 });
		var file = new FormFile(content, 0, content.Length, "files", fileName);

		await page.OnPostUploadFilesAsync("/Images/", new List<IFormFile> { file });

		Assert.False(File.Exists(Path.Combine(contentRoot, "evil.png")));
		Assert.False(File.Exists(Path.Combine(wwwroot, "evil.png")));
		Assert.False(File.Exists(Path.Combine(contentRoot, "..", "evil.png")));
	}

	[Fact]
	public async Task UploadingInsideTheRootStillWorks()
	{
		var page = CreatePage(uploadEnabled: true);
		await using var content = new MemoryStream(new byte[] { 1, 2, 3 });
		var file = new FormFile(content, 0, content.Length, "files", "new image.png");

		await page.OnPostUploadFilesAsync("/Images/", new List<IFormFile> { file });

		Assert.True(File.Exists(Path.Combine(wwwroot, "Images", "new_image.png")));
	}

	private Server.Pages.Admin.FileManager.IndexModel CreatePage(
		bool deleteEnabled = false, bool createDirectoryEnabled = false, bool uploadEnabled = false)
	{
		var settings = new Infrastructure.Settings.ApplicationSettings();
		settings.FileManagerSettings.DeleteItemsEnabled = deleteEnabled;
		settings.FileManagerSettings.CreateDirectoryEnabled = createDirectoryEnabled;
		settings.FileManagerSettings.UploadFilesEnabled = uploadEnabled;

		return new Server.Pages.Admin.FileManager.IndexModel(new FakeEnvironment(contentRoot), settings)
		{
			TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider()),
		};
	}

	private sealed class NullTempDataProvider : ITempDataProvider
	{
		public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
		public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
	}

	private sealed class FakeEnvironment(string contentRootPath) : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = "Test";
		public string ApplicationName { get; set; } = "Test";
		public string ContentRootPath { get; set; } = contentRootPath;
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
	}
}
