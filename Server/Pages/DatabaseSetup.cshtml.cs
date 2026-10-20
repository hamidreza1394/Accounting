using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Infrastructure.DatabaseSetup;
using Infrastructure.Security;

namespace Server.Pages;

/// <summary>
/// First-run database setup. Must not inherit the project CMS type
/// <c>Server.Pages.PageModel</c>, which requires <see cref="Data.DatabaseContext"/>
/// before a connection string exists. Use the framework Razor Pages base type.
/// </summary>
public sealed class DatabaseSetupModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
{
	private const string MasterBootstrapGateKey = "MasterBootstrapGate";

	private readonly DatabaseConnectionSettingsStore _settingsStore;
	private readonly DatabaseConnectionSetupService _setupService;
	private readonly InfrastructureMasterBootstrap _masterBootstrap;
	private readonly IConfiguration _configuration;
	private readonly ILogger<DatabaseSetupModel> _logger;

	public DatabaseSetupModel(
		DatabaseConnectionSettingsStore settingsStore,
		DatabaseConnectionSetupService setupService,
		InfrastructureMasterBootstrap masterBootstrap,
		IConfiguration configuration,
		ILogger<DatabaseSetupModel> logger)
	{
		_settingsStore = settingsStore;
		_setupService = setupService;
		_masterBootstrap = masterBootstrap;
		_configuration = configuration;
		_logger = logger;
	}

	[BindProperty]
	public InputModel Input { get; set; } = new();

	[BindProperty]
	public MasterInputModel MasterInput { get; set; } = new();

	public string SetupTokenFilePath => _settingsStore.SetupTokenFilePath;
	public string? SuccessMessage { get; private set; }
	public string? ErrorMessage { get; private set; }
	public bool SettingsAlreadySaved { get; private set; }
	public bool ShowMasterBootstrap { get; private set; }
	public bool MasterAlreadyConfigured { get; private set; }
	public string? MasterDevelopmentCommands { get; private set; }
	public string? MasterProductionVariables { get; private set; }
	public string? MasterPreparedUsername { get; private set; }

	public IActionResult OnGet()
	{
		if (!string.IsNullOrWhiteSpace(_configuration.GetConnectionString("ConnectionString")))
		{
			return RedirectToPage("/Index");
		}

		SettingsAlreadySaved = _settingsStore.SetupAccessToken is null &&
			System.IO.File.Exists(_settingsStore.SettingsFilePath);

		MasterAlreadyConfigured = InfrastructureMasterBootstrap.IsMasterConfigured(_configuration);
		ShowMasterBootstrap = SettingsAlreadySaved
			&& !MasterAlreadyConfigured
			&& HasValidMasterBootstrapGate();

		if (SettingsAlreadySaved)
		{
			SuccessMessage = "تنظیمات ذخیره شده است. برای فعال شدن اتصال جدید، برنامه را متوقف و دوباره اجرا کنید.";
		}

		return Page();
	}

	public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(_configuration.GetConnectionString("ConnectionString")))
		{
			return RedirectToPage("/Index");
		}

		var expectedTokenValue = _settingsStore.SetupAccessToken;
		if (expectedTokenValue is null)
		{
			SettingsAlreadySaved = System.IO.File.Exists(_settingsStore.SettingsFilePath);
			MasterAlreadyConfigured = InfrastructureMasterBootstrap.IsMasterConfigured(_configuration);
			ShowMasterBootstrap = SettingsAlreadySaved
				&& !MasterAlreadyConfigured
				&& HasValidMasterBootstrapGate();
			SuccessMessage = "تنظیمات قبلاً ذخیره شده است. برای فعال شدن اتصال جدید، برنامه را متوقف و دوباره اجرا کنید.";
			return Page();
		}

		var providedToken = Encoding.UTF8.GetBytes(Input.SetupToken ?? string.Empty);
		var expectedToken = Encoding.UTF8.GetBytes(expectedTokenValue);
		if (providedToken.Length != expectedToken.Length ||
			!CryptographicOperations.FixedTimeEquals(providedToken, expectedToken))
		{
			ModelState.AddModelError(nameof(Input.SetupToken),
				"کد راه‌اندازی معتبر نیست. فایل کد راه‌اندازی را روی همان سرور بررسی کنید.");
		}

		if (!ModelState.IsValid)
		{
			ErrorMessage = "اطلاعات فرم را بررسی کنید.";
			Input.Password = string.Empty;
			return Page();
		}

		var result = await _setupService.ConfigureAsync(
			new DatabaseSetupRequest
			{
				Server = Input.Server,
				Database = Input.Database,
				AuthenticationMode = Input.AuthenticationMode,
				UserName = Input.UserName,
				Password = Input.Password,
				TrustServerCertificate = Input.TrustServerCertificate,
				CreateDatabaseIfMissing = Input.CreateDatabaseIfMissing
			},
			cancellationToken);

		Input.Password = string.Empty;
		if (!result.Succeeded)
		{
			ErrorMessage = result.ErrorMessage ?? "راه‌اندازی پایگاه داده ناموفق بود.";
			return Page();
		}

		SettingsAlreadySaved = true;
		SuccessMessage = result.MigrationsApplied
			? "اتصال تأیید شد و دیتابیس خالی با Migrationهای پروژه آماده شد. برای بارگذاری تنظیمات جدید، برنامه را متوقف و دوباره اجرا کنید."
			: "اتصال تأیید و تنظیمات به‌صورت رمزنگاری‌شده ذخیره شد. دیتابیس از قبل وجود داشت و برای جلوگیری از تغییر ناخواسته داده‌ها، Migration خودکار روی دیتابیس موجود اجرا نشد. برنامه را مجدداً اجرا کنید و وضعیت Migrationها را بررسی کنید.";

		MasterAlreadyConfigured = InfrastructureMasterBootstrap.IsMasterConfigured(_configuration);
		if (!MasterAlreadyConfigured)
		{
			IssueMasterBootstrapGate();
			ShowMasterBootstrap = true;
		}

		_logger.LogInformation("The first-run database setup succeeded. Database credentials were not logged.");
		return Page();
	}

	public IActionResult OnPostPrepareMaster()
	{
		if (!string.IsNullOrWhiteSpace(_configuration.GetConnectionString("ConnectionString")))
		{
			return RedirectToPage("/Index");
		}

		SettingsAlreadySaved = System.IO.File.Exists(_settingsStore.SettingsFilePath);
		MasterAlreadyConfigured = InfrastructureMasterBootstrap.IsMasterConfigured(_configuration);

		if (MasterAlreadyConfigured)
		{
			ShowMasterBootstrap = false;
			SuccessMessage = "حساب زیرساختی از قبل در Configuration پیکربندی شده است و از این مسیر تغییر نمی‌کند.";
			ClearMasterInput();
			return Page();
		}

		if (!SettingsAlreadySaved || !HasValidMasterBootstrapGate())
		{
			ErrorMessage = "تنظیم مدیر زیرساختی فقط بلافاصله پس از راه‌اندازی موفق دیتابیس و با نشست معتبر مجاز است.";
			ShowMasterBootstrap = false;
			ClearMasterInput();
			return Page();
		}

		ShowMasterBootstrap = true;

		var prepareResult = _masterBootstrap.Prepare(
			MasterInput.Username,
			MasterInput.Password,
			MasterInput.PasswordConfirmation);

		ClearMasterInput();

		if (!prepareResult.Succeeded)
		{
			ErrorMessage = prepareResult.ErrorMessage;
			return Page();
		}

		MasterPreparedUsername = prepareResult.Username;
		MasterDevelopmentCommands = prepareResult.DevelopmentCommands;
		MasterProductionVariables = prepareResult.ProductionEnvironmentVariables;
		SuccessMessage = "هش رمز مدیر زیرساختی آماده شد. مقادیر را در User Secrets (توسعه) یا متغیر محیطی / Secret Store (تولید) قرار دهید و برنامه را Restart کنید. رمز خام ذخیره نشده است.";

		ClearMasterBootstrapGate();
		ShowMasterBootstrap = false;

		_logger.LogInformation(
			"Infrastructure Master bootstrap credentials were prepared for operator configuration. No password or hash was written to application logs.");

		return Page();
	}

	private void IssueMasterBootstrapGate()
	{
		var gate = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
		TempData[MasterBootstrapGateKey] = gate;
	}

	private bool HasValidMasterBootstrapGate()
	{
		return TempData.Peek(MasterBootstrapGateKey) is string gate
			&& !string.IsNullOrWhiteSpace(gate);
	}

	private void ClearMasterBootstrapGate()
	{
		TempData.Remove(MasterBootstrapGateKey);
	}

	private void ClearMasterInput()
	{
		MasterInput.Password = string.Empty;
		MasterInput.PasswordConfirmation = string.Empty;
		ModelState.Remove(nameof(MasterInput) + "." + nameof(MasterInputModel.Password));
		ModelState.Remove(nameof(MasterInput) + "." + nameof(MasterInputModel.PasswordConfirmation));
	}

	public sealed class InputModel
	{
		[Required(ErrorMessage = "کد راه‌اندازی الزامی است.")]
		public string SetupToken { get; set; } = string.Empty;

		[Required(ErrorMessage = "نام سرور الزامی است.")]
		[StringLength(256, MinimumLength = 1)]
		public string Server { get; set; } = ".\\SQLSERVER2025";

		[Required(ErrorMessage = "نام دیتابیس الزامی است.")]
		[StringLength(128, MinimumLength = 1)]
		public string Database { get; set; } = "TarazAvaDb";

		[Required]
		[RegularExpression("^(Windows|Sql)$", ErrorMessage = "نوع احراز هویت معتبر نیست.")]
		public string AuthenticationMode { get; set; } = "Windows";

		[StringLength(128)]
		public string UserName { get; set; } = string.Empty;

		[StringLength(256)]
		public string Password { get; set; } = string.Empty;

		public bool TrustServerCertificate { get; set; }
		public bool CreateDatabaseIfMissing { get; set; } = true;
	}

	public sealed class MasterInputModel
	{
		[Required(ErrorMessage = "نام کاربری مدیر زیرساختی الزامی است.")]
		[StringLength(InfrastructureMasterBootstrap.MaximumUsernameLength, MinimumLength = 1)]
		public string Username { get; set; } = string.Empty;

		[Required(ErrorMessage = "رمز عبور الزامی است.")]
		[StringLength(InfrastructureMasterBootstrap.MaximumPasswordLength, MinimumLength = InfrastructureMasterBootstrap.MinimumPasswordLength,
			ErrorMessage = "رمز عبور باید بین {2} و {1} نویسه باشد.")]
		[DataType(DataType.Password)]
		public string Password { get; set; } = string.Empty;

		[Required(ErrorMessage = "تکرار رمز عبور الزامی است.")]
		[DataType(DataType.Password)]
		[Compare(nameof(Password), ErrorMessage = "رمز عبور و تکرار آن یکسان نیستند.")]
		public string PasswordConfirmation { get; set; } = string.Empty;
	}
}
