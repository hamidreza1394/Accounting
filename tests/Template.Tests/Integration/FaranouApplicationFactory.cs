using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Template.Tests.Integration;

/// <summary>
/// Chooses the database of the HTTP integration tests.
/// </summary>
/// <remarks>
/// <para>
/// When <c>FARANOU_TEST_SQLSERVER</c> holds a SQL Server connection string (CI starts a SQL
/// Server container for it), the tests run against <b>real SQL Server</b>: the schema is created
/// by the project's EF Core migrations once per test run. For safety the database name must
/// start with <c>FaranouTests</c>, because it is dropped and re-created.
/// </para>
/// <para>
/// Without the variable the tests use the EF Core in-memory provider. That still exercises
/// the real middleware, authentication, authorization, anti-forgery, routing and rate limiting,
/// but it is <b>not equivalent to SQL Server</b> (no constraints, no SQL translation, no
/// migrations).
/// </para>
/// <para>
/// The application reads its connection string while the host is being built, so the value is
/// provided through the standard environment variable before the first host starts. All tests
/// therefore share one database and create their own, uniquely named data.
/// </para>
/// </remarks>
internal static class IntegrationDatabase
{
	public const string SqlServerVariable = "FARANOU_TEST_SQLSERVER";

	private const string InMemoryPlaceholder =
		"Server=in-memory.invalid;Database=FaranouInMemory;Integrated Security=false;User Id=unused;Password=unused";

	private static readonly SemaphoreSlim SchemaGate = new(1, 1);
	private static readonly object ProcessGate = new();
	private static bool schemaReady;
	private static bool processConfigured;

	public static string? SqlServerConnectionString =>
		Environment.GetEnvironmentVariable(SqlServerVariable);

	public static bool UsesSqlServer => !string.IsNullOrWhiteSpace(SqlServerConnectionString);

	public static string ProviderDescription => UsesSqlServer ? "SQL Server" : "EF Core InMemory (not SQL Server)";

	public static void ConfigureProcess()
	{
		lock (ProcessGate)
		{
			if (processConfigured)
			{
				return;
			}

			if (UsesSqlServer)
			{
				var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(SqlServerConnectionString);

				if (!builder.InitialCatalog.StartsWith("FaranouTests", StringComparison.OrdinalIgnoreCase))
				{
					throw new InvalidOperationException(
						$"{SqlServerVariable} must name a disposable database whose name starts with 'FaranouTests'; it is dropped by the tests.");
				}
			}

			Environment.SetEnvironmentVariable(
				"ConnectionStrings__ConnectionString",
				UsesSqlServer ? SqlServerConnectionString : InMemoryPlaceholder);

			processConfigured = true;
		}
	}

	public static async Task EnsureSchemaAsync(IServiceProvider services)
	{
		if (!UsesSqlServer)
		{
			return;
		}

		await SchemaGate.WaitAsync();

		try
		{
			if (schemaReady)
			{
				return;
			}

			using var scope = services.CreateScope();
			var database = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>().Database;

			await database.EnsureDeletedAsync();
			await database.MigrateAsync();

			schemaReady = true;
		}
		finally
		{
			SchemaGate.Release();
		}
	}
}

/// <summary>
/// Hosts the real application (Program.cs: middleware, cookie authentication, authorization,
/// anti-forgery, rate limiting, routing, Razor Pages and the API) in memory.
/// </summary>
public class FaranouApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
	static FaranouApplicationFactory()
	{
		IntegrationDatabase.ConfigureProcess();
	}

	private readonly string inMemoryDatabaseName = $"faranou-it-{Guid.NewGuid():N}";

	/// <summary>Configuration overrides; generous rate limits so ordinary tests are never throttled.</summary>
	protected virtual IDictionary<string, string?> Settings => new Dictionary<string, string?>
	{
		["RateLimiting:Api:PermitLimit"] = "100000",
		["RateLimiting:ApiSensitive:PermitLimit"] = "100000",
		["RateLimiting:Login:PermitLimit"] = "100000",
	};

	protected virtual void ConfigureServices(IServiceCollection services)
	{
	}

	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		builder.UseEnvironment("Testing");
		builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Settings));

		builder.ConfigureTestServices(services =>
		{
			if (!IntegrationDatabase.UsesSqlServer)
			{
				services.RemoveAll<DbContextOptions<Data.DatabaseContext>>();
				services.RemoveAll<IDbContextOptionsConfiguration<Data.DatabaseContext>>();
				services.AddDbContext<Data.DatabaseContext>(options =>
					options.UseInMemoryDatabase(inMemoryDatabaseName));
			}

			ConfigureServices(services);
		});
	}

	public async ValueTask InitializeAsync()
	{
		await IntegrationDatabase.EnsureSchemaAsync(Services);
	}

	/// <summary>A browser-like client: HTTPS, cookies kept, redirects not followed.</summary>
	public HttpClient CreateBrowser()
	{
		return CreateClient(new WebApplicationFactoryClientOptions
		{
			BaseAddress = new Uri("https://localhost"),
			AllowAutoRedirect = false,
			HandleCookies = true,
		});
	}

	public async Task<T> WithDatabaseAsync<T>(Func<Data.DatabaseContext, Task<T>> action)
	{
		using var scope = Services.CreateScope();
		var databaseContext = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
		return await action(databaseContext);
	}

	public Task WithDatabaseAsync(Func<Data.DatabaseContext, Task> action)
	{
		return WithDatabaseAsync(async databaseContext =>
		{
			await action(databaseContext);
			return true;
		});
	}
}

internal sealed record SeededUser(Guid Id, string Username, string Password);

internal sealed record SeededCompany(Guid Id, string Name, Guid RoleId);

/// <summary>Uniquely named test data, so tests can share one database.</summary>
internal static class TestData
{
	private static readonly Infrastructure.Security.PasswordHashService Hasher = new();

	public static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..16];

	public static async Task<SeededUser> AddUserAsync(Data.DatabaseContext database, bool isActive = true)
	{
		var username = Unique("it");
		var password = $"Pw-{Guid.NewGuid():N}";

		var user = new Domain.User(emailAddress: $"{username}@example.test")
		{
			Username = username,
			Password = Hasher.Hash(password: password),
			IsActive = isActive,
		};

		database.Users.Add(user);
		await database.SaveChangesAsync();

		return new SeededUser(user.Id, username, password);
	}

	public static async Task<SeededCompany> AddCompanyAsync(
		Data.DatabaseContext database, string name, bool isActive = true, bool roleIsActive = true)
	{
		var company = new Domain.Company { Name = name, IsActive = isActive };
		var role = new Domain.CompanyRole
		{
			TenantId = company.Id,
			Company = company,
			Code = "accountant",
			Name = "حسابدار",
			IsActive = roleIsActive,
		};

		database.Companies.Add(company);
		database.CompanyRoles.Add(role);
		await database.SaveChangesAsync();

		return new SeededCompany(company.Id, name, role.Id);
	}

	public static async Task AddMembershipAsync(
		Data.DatabaseContext database, SeededUser user, SeededCompany company, bool isActive = true)
	{
		database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
		{
			UserId = user.Id,
			TenantId = company.Id,
			RoleId = company.RoleId,
			IsActive = isActive,
		});

		await database.SaveChangesAsync();
	}

	public static async Task<Guid> AddFiscalPeriodAsync(
		Data.DatabaseContext database,
		SeededCompany company,
		string title,
		DateOnly start,
		DateOnly end,
		Domain.FiscalPeriodStatus status = Domain.FiscalPeriodStatus.Open)
	{
		var period = new Domain.FiscalPeriod
		{
			TenantId = company.Id,
			Title = title,
			StartDate = start,
			EndDate = end,
			Status = status,
		};

		database.FiscalPeriods.Add(period);

		await database.SaveChangesAsync();

		return period.Id;
	}
}

/// <summary>Drives the real Razor sign-in form, including its anti-forgery token.</summary>
internal static partial class Browser
{
	[GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
	private static partial Regex TokenInHiddenField();

	[GeneratedRegex("type=\"hidden\" name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
	private static partial Regex TokenInHiddenFieldAlternateOrder();

	public static async Task<string> ReadFormTokenAsync(HttpClient client, string path)
	{
		var response = await client.GetAsync(path);
		var html = await response.Content.ReadAsStringAsync();

		Assert.True(response.IsSuccessStatusCode, $"GET {path} returned {(int)response.StatusCode}.");

		var match = TokenInHiddenField().Match(html);

		if (!match.Success)
		{
			match = TokenInHiddenFieldAlternateOrder().Match(html);
		}

		Assert.True(match.Success, $"No anti-forgery token found on {path}.");

		return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
	}

	public static async Task<HttpResponseMessage> PostLoginAsync(
		HttpClient client, string username, string password, bool rememberMe = false, string? returnUrl = null)
	{
		var token = await ReadFormTokenAsync(client, "/Account/Login");

		var fields = new List<KeyValuePair<string, string>>
		{
			new("ViewModel.Username", username),
			new("ViewModel.Password", password),
			new("ViewModel.RememberMe", rememberMe ? "true" : "false"),
			new("__RequestVerificationToken", token),
		};

		if (returnUrl is not null)
		{
			fields.Add(new("ReturnUrl", returnUrl));
		}

		return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(fields));
	}

	public static async Task SignInAsync(HttpClient client, SeededUser user)
	{
		var response = await PostLoginAsync(client, user.Username, user.Password);

		Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
	}
}
