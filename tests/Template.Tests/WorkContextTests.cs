using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Template.Tests;

/// <summary>Builds users, companies and fiscal periods in an isolated in-memory database.</summary>
internal sealed class WorkContextFixture : IDisposable
{
	private static readonly DateOnly Start = new(2026, 3, 21);
	private static readonly DateOnly End = new(2027, 3, 20);

	public WorkContextFixture()
	{
		Database = new Data.DatabaseContext(new DbContextOptionsBuilder<Data.DatabaseContext>()
			.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
			.Options);

		User = new Domain.User(emailAddress: "u@example.test") { Username = "u", IsActive = true };
		Database.Users.Add(User);
		Database.SaveChanges();

		Cookie = new Infrastructure.Tenancy.WorkContextCookie(new EphemeralDataProtectionProvider(), TimeProvider.System);
		Service = new Infrastructure.Tenancy.WorkContextService(
			Database, new Infrastructure.Tenancy.CompanyAccessService(Database), Cookie);
	}

	public Data.DatabaseContext Database { get; }

	public Domain.User User { get; }

	public Infrastructure.Tenancy.WorkContextCookie Cookie { get; }

	public Infrastructure.Tenancy.WorkContextService Service { get; }

	public (Domain.Company Company, List<Domain.FiscalPeriod> Periods) AddCompany(
		bool member = true,
		bool membershipActive = true,
		bool companyActive = true,
		params Domain.FiscalPeriodStatus[] periodStatuses)
	{
		var company = new Domain.Company { Name = "شرکت " + Guid.NewGuid().ToString("N")[..6], IsActive = companyActive };
		var role = new Domain.CompanyRole { TenantId = company.Id, Company = company, Code = "r", Name = "نقش", IsActive = true };
		Database.Companies.Add(company);
		Database.CompanyRoles.Add(role);

		if (member)
		{
			Database.UserCompanyMemberships.Add(new Domain.UserCompanyMembership
			{
				UserId = User.Id,
				TenantId = company.Id,
				RoleId = role.Id,
				IsActive = membershipActive,
			});
		}

		var periods = new List<Domain.FiscalPeriod>();
		var year = 0;

		foreach (var status in periodStatuses)
		{
			var period = new Domain.FiscalPeriod
			{
				TenantId = company.Id,
				Title = $"دوره {year}",
				StartDate = Start.AddYears(year),
				EndDate = End.AddYears(year),
				Status = status,
			};
			periods.Add(period);
			Database.FiscalPeriods.Add(period);
			year++;
		}

		Database.SaveChanges();
		Database.ChangeTracker.Clear();

		return (company, periods);
	}

	public void Dispose() => Database.Dispose();
}

/// <summary>The entry decision: direct entry, selection, or a refusal.</summary>
public sealed class WorkContextOptionsTests
{
	private static readonly Domain.FiscalPeriodStatus Open = Domain.FiscalPeriodStatus.Open;
	private static readonly Domain.FiscalPeriodStatus Closed = Domain.FiscalPeriodStatus.Closed;
	private static readonly Domain.FiscalPeriodStatus Archived = Domain.FiscalPeriodStatus.Archived;

	[Fact]
	public async Task NoCompany_ProducesNoCompany()
	{
		using var fixture = new WorkContextFixture();

		var options = await fixture.Service.GetOptionsAsync(fixture.User.Id);

		Assert.Equal(Infrastructure.Tenancy.EntryMode.NoCompany, options.Entry);
		Assert.Empty(options.Companies);
	}

	[Fact]
	public async Task OneCompanyWithOneOpenPeriod_IsDirectEntry()
	{
		using var fixture = new WorkContextFixture();
		var (company, periods) = fixture.AddCompany(true, true, true, Open);

		var options = await fixture.Service.GetOptionsAsync(fixture.User.Id);

		Assert.Equal(Infrastructure.Tenancy.EntryMode.DirectEntry, options.Entry);
		Assert.Equal(company.Id, options.DirectCompanyId);
		Assert.Equal(periods[0].Id, options.DirectFiscalPeriodId);
	}

	[Fact]
	public async Task ClosedAndArchivedPeriods_AreIgnoredForDirectEntry_ButStillListed()
	{
		using var fixture = new WorkContextFixture();
		var (_, periods) = fixture.AddCompany(true, true, true, Closed, Open, Archived);

		var options = await fixture.Service.GetOptionsAsync(fixture.User.Id);

		Assert.Equal(Infrastructure.Tenancy.EntryMode.DirectEntry, options.Entry);
		Assert.Equal(periods[1].Id, options.DirectFiscalPeriodId);
		var listed = Assert.Single(options.Companies).FiscalPeriods;
		Assert.Equal(3, listed.Count);
		Assert.Single(listed, period => period.IsSelectable);
	}

	[Fact]
	public async Task OneCompanyWithSeveralOpenPeriods_RequiresSelection()
	{
		using var fixture = new WorkContextFixture();
		fixture.AddCompany(true, true, true, Open, Open);

		var options = await fixture.Service.GetOptionsAsync(fixture.User.Id);

		Assert.Equal(Infrastructure.Tenancy.EntryMode.SelectionRequired, options.Entry);
		Assert.Null(options.DirectCompanyId);
	}

	[Fact]
	public async Task SeveralCompanies_RequireSelection_EvenWhenEachHasOneOpenPeriod()
	{
		using var fixture = new WorkContextFixture();
		fixture.AddCompany(true, true, true, Open);
		fixture.AddCompany(true, true, true, Open);

		var options = await fixture.Service.GetOptionsAsync(fixture.User.Id);

		Assert.Equal(Infrastructure.Tenancy.EntryMode.SelectionRequired, options.Entry);
		Assert.Equal(2, options.Companies.Count);
	}

	[Fact]
	public async Task ACompanyWithoutAPeriod_OrWithOnlyClosedOnes_IsNoEligiblePeriod()
	{
		using var fixture = new WorkContextFixture();
		fixture.AddCompany(true, true, true);
		fixture.AddCompany(true, true, true, Closed, Archived);

		var options = await fixture.Service.GetOptionsAsync(fixture.User.Id);

		Assert.Equal(Infrastructure.Tenancy.EntryMode.NoEligiblePeriod, options.Entry);
		Assert.Equal(2, options.Companies.Count);
	}

	[Fact]
	public async Task InactiveMembershipsAndCompanies_AreNotOffered_AndForeignCompaniesNeverAppear()
	{
		using var fixture = new WorkContextFixture();
		fixture.AddCompany(true, false, true, Open);
		fixture.AddCompany(true, true, false, Open);
		fixture.AddCompany(false, true, true, Open);

		var options = await fixture.Service.GetOptionsAsync(fixture.User.Id);

		Assert.Equal(Infrastructure.Tenancy.EntryMode.NoCompany, options.Entry);
		Assert.Empty(options.Companies);
	}

	[Fact]
	public async Task AnOtherUser_SeesNothingOfThisUsersCompanies()
	{
		using var fixture = new WorkContextFixture();
		fixture.AddCompany(true, true, true, Open);

		var options = await fixture.Service.GetOptionsAsync(Guid.NewGuid());

		Assert.Equal(Infrastructure.Tenancy.EntryMode.NoCompany, options.Entry);
	}
}

/// <summary>The validation of a requested selection.</summary>
public sealed class WorkContextSelectionTests
{
	private static HttpContext NewHttpContext(WorkContextFixture fixture)
	{
		var services = new ServiceCollection();
		services.AddSingleton<IHostEnvironment>(new FakeEnvironment());

		return new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
	}

	private sealed class FakeEnvironment : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = Environments.Development;
		public string ApplicationName { get; set; } = "Tests";
		public string ContentRootPath { get; set; } = ".";
		public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
			new Microsoft.Extensions.FileProviders.NullFileProvider();
	}

	[Fact]
	public async Task AValidChoice_IsAccepted()
	{
		using var fixture = new WorkContextFixture();
		var (company, periods) = fixture.AddCompany(true, true, true, Domain.FiscalPeriodStatus.Open);

		var result = await fixture.Service.SelectAsync(
			NewHttpContext(fixture), fixture.User.Id, Guid.NewGuid(), company.Id, periods[0].Id);

		Assert.Equal(Infrastructure.Tenancy.SelectionOutcome.Selected, result.Outcome);
		Assert.Equal(company.Id, result.Context!.Company.CompanyId);
		Assert.Equal(periods[0].Id, result.Context.FiscalPeriod.Id);
	}

	[Fact]
	public async Task ACompanyTheUserIsNotAMemberOf_IsRefused()
	{
		using var fixture = new WorkContextFixture();
		var (company, periods) = fixture.AddCompany(false, true, true, Domain.FiscalPeriodStatus.Open);

		var result = await fixture.Service.SelectAsync(
			NewHttpContext(fixture), fixture.User.Id, Guid.NewGuid(), company.Id, periods[0].Id);

		Assert.Equal(Infrastructure.Tenancy.SelectionOutcome.CompanyNotAccessible, result.Outcome);
		Assert.Null(result.Context);
	}

	[Fact]
	public async Task APeriodOfAnotherCompany_IsRefused_AsNotFound()
	{
		using var fixture = new WorkContextFixture();
		var (own, _) = fixture.AddCompany(true, true, true, Domain.FiscalPeriodStatus.Open);
		var (_, foreignPeriods) = fixture.AddCompany(false, true, true, Domain.FiscalPeriodStatus.Open);

		var result = await fixture.Service.SelectAsync(
			NewHttpContext(fixture), fixture.User.Id, Guid.NewGuid(), own.Id, foreignPeriods[0].Id);

		Assert.Equal(Infrastructure.Tenancy.SelectionOutcome.PeriodNotFound, result.Outcome);
	}

	[Fact]
	public async Task ANonExistingPeriod_AndAnEmptyIdentifier_AreRefused()
	{
		using var fixture = new WorkContextFixture();
		var (company, _) = fixture.AddCompany(true, true, true, Domain.FiscalPeriodStatus.Open);

		var missing = await fixture.Service.SelectAsync(
			NewHttpContext(fixture), fixture.User.Id, Guid.NewGuid(), company.Id, Guid.NewGuid());
		var empty = await fixture.Service.SelectAsync(
			NewHttpContext(fixture), fixture.User.Id, Guid.NewGuid(), company.Id, Guid.Empty);

		Assert.Equal(Infrastructure.Tenancy.SelectionOutcome.PeriodNotFound, missing.Outcome);
		Assert.Equal(Infrastructure.Tenancy.SelectionOutcome.PeriodNotFound, empty.Outcome);
	}

	[Theory]
	[InlineData(Domain.FiscalPeriodStatus.Closed)]
	[InlineData(Domain.FiscalPeriodStatus.Archived)]
	public async Task ANonOpenPeriod_IsRefused(Domain.FiscalPeriodStatus status)
	{
		using var fixture = new WorkContextFixture();
		var (company, periods) = fixture.AddCompany(true, true, true, status);

		var result = await fixture.Service.SelectAsync(
			NewHttpContext(fixture), fixture.User.Id, Guid.NewGuid(), company.Id, periods[0].Id);

		Assert.Equal(Infrastructure.Tenancy.SelectionOutcome.PeriodNotSelectable, result.Outcome);
	}

	[Fact]
	public async Task AnInactiveCompany_IsRefused()
	{
		using var fixture = new WorkContextFixture();
		var (company, periods) = fixture.AddCompany(true, true, false, Domain.FiscalPeriodStatus.Open);

		var result = await fixture.Service.SelectAsync(
			NewHttpContext(fixture), fixture.User.Id, Guid.NewGuid(), company.Id, periods[0].Id);

		Assert.Equal(Infrastructure.Tenancy.SelectionOutcome.CompanyNotAccessible, result.Outcome);
	}
}

/// <summary>The protected choice cookie: it is bound to the user and to one sign-in.</summary>
public sealed class WorkContextCookieTests
{
	private sealed class FakeEnvironment : IHostEnvironment
	{
		public string EnvironmentName { get; set; } = Environments.Development;
		public string ApplicationName { get; set; } = "Tests";
		public string ContentRootPath { get; set; } = ".";
		public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
			new Microsoft.Extensions.FileProviders.NullFileProvider();
	}

	private static DefaultHttpContext NewContext(string? cookieHeader = null)
	{
		var services = new ServiceCollection();
		services.AddSingleton<IHostEnvironment>(new FakeEnvironment());
		var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

		if (cookieHeader is not null)
		{
			context.Request.Headers.Cookie = cookieHeader;
		}

		return context;
	}

	private static string WrittenCookieHeader(Infrastructure.Tenancy.WorkContextCookie cookie, Guid user, Guid session, Guid company, Guid period)
	{
		var context = NewContext();
		cookie.Write(context, user, session, company, period);
		var setCookie = context.Response.Headers.SetCookie.ToString();
		return setCookie.Split(';')[0];
	}

	private static Infrastructure.Tenancy.WorkContextCookie NewCookie(TimeProvider? timeProvider = null)
		=> new(new EphemeralDataProtectionProvider(), timeProvider ?? TimeProvider.System);

	[Fact]
	public void TheChoice_IsReadBackForTheSameUserAndSignIn()
	{
		var cookie = NewCookie();
		var (user, session, company, period) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var header = WrittenCookieHeader(cookie, user, session, company, period);

		var read = cookie.Read(NewContext(header), user, session);

		Assert.Equal(company, read!.Value.CompanyId);
		Assert.Equal(period, read.Value.FiscalPeriodId);
	}

	[Fact]
	public void ANewSignIn_OrAnotherUser_CannotUseTheChoice()
	{
		var cookie = NewCookie();
		var (user, session, company, period) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		var header = WrittenCookieHeader(cookie, user, session, company, period);

		Assert.Null(cookie.Read(NewContext(header), user, Guid.NewGuid()));
		Assert.Null(cookie.Read(NewContext(header), Guid.NewGuid(), session));
	}

	[Fact]
	public void ATamperedForeignOrGarbageValue_IsTreatedAsNothingSelected()
	{
		var cookie = NewCookie();
		var (user, session) = (Guid.NewGuid(), Guid.NewGuid());
		var header = WrittenCookieHeader(cookie, user, session, Guid.NewGuid(), Guid.NewGuid());
		var tampered = header[..^3] + (header.EndsWith("AAA", StringComparison.Ordinal) ? "BBB" : "AAA");
		var otherKeyRing = WrittenCookieHeader(NewCookie(), user, session, Guid.NewGuid(), Guid.NewGuid());

		Assert.Null(cookie.Read(NewContext(tampered), user, session));
		Assert.Null(cookie.Read(NewContext(otherKeyRing), user, session));
		Assert.Null(cookie.Read(NewContext(".Faranou.WorkContext=not-protected"), user, session));
		Assert.Null(cookie.Read(NewContext(), user, session));
	}

	[Fact]
	public void TheChoice_ExpiresAfterItsMaximumAge()
	{
		// The data-protection layer validates the expiry against the real clock, so the choice is
		// written "in the past": a cookie issued longer ago than MaxAge is no longer accepted.
		var issuedLongAgo = new ManualTimeProvider(
			DateTimeOffset.UtcNow - Infrastructure.Tenancy.WorkContextCookie.MaxAge - TimeSpan.FromMinutes(1));
		var cookie = NewCookie(issuedLongAgo);
		var (user, session) = (Guid.NewGuid(), Guid.NewGuid());
		var header = WrittenCookieHeader(cookie, user, session, Guid.NewGuid(), Guid.NewGuid());

		Assert.Null(cookie.Read(NewContext(header), user, session));
	}

	[Fact]
	public void TheSignInIdentifier_IsReadOnlyFromAValidClaim()
	{
		var id = Guid.NewGuid();
		ClaimsPrincipal With(string? value) => new(new ClaimsIdentity(
			value is null ? Array.Empty<Claim>() : new[] { new Claim("SessionId", value) }, "test"));

		Assert.Equal(id, Infrastructure.Tenancy.WorkContextCookie.GetSessionId(With(id.ToString())));
		Assert.Null(Infrastructure.Tenancy.WorkContextCookie.GetSessionId(With(null)));
		Assert.Null(Infrastructure.Tenancy.WorkContextCookie.GetSessionId(With("nope")));
		Assert.Null(Infrastructure.Tenancy.WorkContextCookie.GetSessionId(With(Guid.Empty.ToString())));
	}

	private sealed class ManualTimeProvider : TimeProvider
	{
		private DateTimeOffset now;

		public ManualTimeProvider(DateTimeOffset start) => now = start;

		public override DateTimeOffset GetUtcNow() => now;
	}
}

/// <summary>The endpoints' handling of accounts that cannot hold a choice.</summary>
public sealed class WorkContextEndpointGuardTests
{
	private sealed class AllowAllAntiforgery : IAntiforgery
	{
		public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => new("r", "c", "f", "h");
		public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => new("r", "c", "f", "h");
		public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);
		public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
		public void SetCookieTokenAndHeader(HttpContext httpContext) { }
	}

	private static async Task<object?> PostAsync(ClaimsPrincipal principal)
	{
		using var fixture = new WorkContextFixture();
		var result = await Server.Api.WorkContextEndpoints.SelectAsync(
			new DefaultHttpContext(),
			principal,
			new Server.Api.SelectContextRequest(Guid.NewGuid(), Guid.NewGuid()),
			new AllowAllAntiforgery(),
			fixture.Service,
			CancellationToken.None);

		return result.Result;
	}

	[Fact]
	public async Task ASessionIssuedBeforeTheSignInIdentifierExisted_MustSignInAgain()
	{
		var principal = TenantFixture.CompanyUser(Guid.NewGuid());

		Assert.IsType<UnauthorizedHttpResult>(await PostAsync(principal));
	}

	[Fact]
	public async Task TheInfrastructureAccount_HasNothingToChoose()
	{
		Assert.IsType<NotFound>(await PostAsync(TenantFixture.InfrastructureAccount()));
	}
}
