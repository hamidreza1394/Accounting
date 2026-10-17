using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Server.Pages
{
	/// <summary>
	/// The protected destination after a successful sign-in. Only an authenticated
	/// user can open the page; it shows the verified account and the companies the
	/// user is an active member of (memberships are read from the database, never
	/// from client input).
	/// </summary>
	[Microsoft.AspNetCore.Authorization.Authorize]
	public class IndexModel : Infrastructure.BasePageModelWithDatabaseContext
	{
		#region Constructor
		public IndexModel
			(Data.DatabaseContext databaseContext) :
			base(databaseContext: databaseContext)
		{
			Companies =
				new System.Collections.Generic.List<CompanyViewModel>();
		}
		#endregion /Constructor

		#region Properties
		public string? FullName { get; set; }

		public string? Username { get; set; }

		public System.Collections.Generic.List<CompanyViewModel> Companies { get; set; }
		#endregion /Properties

		#region Methods
		public async System.Threading.Tasks.Task
			<Microsoft.AspNetCore.Mvc.IActionResult> OnGetAsync()
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
				if (!string.IsNullOrWhiteSpace(value: Username))
				{
					var fixedUsername = Username.Trim().ToLower();

					var userId =
						await DatabaseContext.Users
						.Where(current => current.Username != null)
						.Where(current => current.Username!.ToLower() == fixedUsername)
						.Select(current => current.Id)
						.FirstOrDefaultAsync();

					if (userId != System.Guid.Empty)
					{
						Companies =
							await DatabaseContext.UserCompanyMemberships
							.Where(current => current.UserId == userId)
							.Where(current => current.IsActive)
							.Where(current => current.Company.IsActive)
							.OrderBy(current => current.Company.Name)
							.Select(current => new CompanyViewModel
							{
								Name = current.Company.Name,
								RoleName = current.Role.Name,
							})
							.ToListAsync();
					}
				}
			}
			catch (System.Exception)
			{
				// A user of a database that is not migrated yet still gets the page;
				// the company list simply stays empty.
				Companies =
					new System.Collections.Generic.List<CompanyViewModel>();
			}

			return Page();
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
