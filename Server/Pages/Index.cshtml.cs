using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Server.Pages
{
	/// <summary>
	/// The protected home page after sign-in. It shows the verified account and the
	/// companies the user may work in. The companies are resolved on the server by
	/// <see cref="Infrastructure.Tenancy.CompanyAccessService" /> from the active
	/// memberships in the database, never from client input or cookie claims.
	/// </summary>
	[Microsoft.AspNetCore.Authorization.Authorize]
	public class IndexModel : Infrastructure.BasePageModelWithDatabaseContext
	{
		#region Constructor
		public IndexModel
			(Data.DatabaseContext databaseContext,
			Infrastructure.Tenancy.CompanyAccessService companyAccessService,
			ILogger<IndexModel> logger) :
			base(databaseContext: databaseContext)
		{
			CompanyAccessService = companyAccessService;
			Logger = logger;

			Companies =
				new System.Collections.Generic.List<CompanyViewModel>();
		}
		#endregion /Constructor

		#region Properties
		public string? FullName { get; set; }

		public string? Username { get; set; }

		public System.Collections.Generic.List<CompanyViewModel> Companies { get; set; }

		/// <summary>
		/// True when the companies could not be loaded. The page then shows an error
		/// instead of pretending that the user has no company.
		/// </summary>
		public bool CompaniesLoadFailed { get; set; }

		private Infrastructure.Tenancy.CompanyAccessService CompanyAccessService { get; }

		private ILogger<IndexModel> Logger { get; }
		#endregion /Properties

		#region Methods
		public async System.Threading.Tasks.Task
			<Microsoft.AspNetCore.Mvc.IActionResult> OnGetAsync
			(System.Threading.CancellationToken cancellationToken)
		{
			Username = User.Identity?.Name;

			FullName =
				User.FindFirst(type: "FullName")?.Value;

			if (string.IsNullOrWhiteSpace(value: FullName))
			{
				FullName = Username;
			}

			try
			{
				var userId = await ResolveUserIdAsync(cancellationToken);

				if (userId.HasValue)
				{
					var companies =
						await CompanyAccessService.GetAccessibleCompaniesAsync
						(userId: userId.Value, cancellationToken: cancellationToken);

					Companies = companies
						.Select(company => new CompanyViewModel
						{
							Name = company.CompanyName,
							RoleName = company.RoleName,
						})
						.ToList();
				}
			}
			catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (System.Exception exception)
			{
				// The failure is recorded and shown; it is never turned into an empty list
				// that would look like "you have no company".
				Logger.LogError
					(exception: exception,
					message: "The companies of the signed-in user could not be loaded.");

				CompaniesLoadFailed = true;
				Companies = new System.Collections.Generic.List<CompanyViewModel>();
			}

			return Page();
		}

		/// <summary>
		/// The identifier claim is issued by the current sign-in. Sessions created by an
		/// older version only carry the username, so the account is looked up by it.
		/// The infrastructure account is not a company user and has no companies.
		/// </summary>
		private async System.Threading.Tasks.Task<System.Guid?> ResolveUserIdAsync
			(System.Threading.CancellationToken cancellationToken)
		{
			var userId =
				Infrastructure.Tenancy.CompanyAccessService.GetUserId(principal: User);

			if (userId.HasValue)
			{
				return userId;
			}

			var accountKind =
				User.FindFirst(type: Infrastructure.Tenancy.CompanyAccessService.AccountKindClaimType)?.Value;

			if (!string.Equals(accountKind, Infrastructure.Tenancy.CompanyAccessService.UserAccountKind,
				System.StringComparison.Ordinal) ||
				string.IsNullOrWhiteSpace(value: Username))
			{
				return null;
			}

			var fixedUsername = Username.Trim().ToLower();

			var legacyUserId =
				await DatabaseContext.Users
				.AsNoTracking()
				.Where(current => current.Username != null)
				.Where(current => current.Username!.ToLower() == fixedUsername)
				.Select(current => current.Id)
				.FirstOrDefaultAsync(cancellationToken);

			return legacyUserId == System.Guid.Empty ? null : legacyUserId;
		}
		#endregion /Methods

		#region CompanyViewModel
		public sealed class CompanyViewModel : object
		{
			public string? Name { get; set; }

			public string? RoleName { get; set; }
		}
		#endregion /CompanyViewModel
	}
}
