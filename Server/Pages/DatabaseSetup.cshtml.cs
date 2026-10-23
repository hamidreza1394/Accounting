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

	/// <summary>Result of loading the settings at start-up (null only if the host did not load them).</summary>
	public DatabaseSettingsStatus SettingsStatus => _settingsStore.LastLoad?.Status ?? DatabaseSettingsStatus.NotConfigured;
	public string SettingsDirectory => _settingsStore.SettingsDirectory;

	/// <summary>AccessDenied/UnsupportedVersion: no form, only status, folder and remedy.</summary>
	public bool IsBlocked => _settingsStore.LastLoad?.IsBlocked ?? false;

	/// <summary>A settings file exists but is unusable: replacing it needs Input.ConfirmReplace.</summary>
	public bool RequiresReplaceConfirmation => _settingsStore.LastLoad?.RequiresReplaceConfirmation ?? false;

	public string? StatusTitle { get; private set; }
	public string? StatusRemedy { get; private set; }

	// Details of the last failed setup attempt (all values are safe to show).
	public string? FailureRemedy { get; private set; }
	public string? TrackingId { get; private set; }
	public DatabaseSetupErrorCode FailureCode { get; private set; }
	public DatabaseSetupStage? FailedStage { get; private set; }
	public IReadOnlyList<DatabaseSetupStageOutcome> StageOutcomes { get; private set; } = [];
	public IReadOnlyList<string> Warnings { get; private set; } = [];

	public IActionResult OnGet()
	{
		if (!string.IsNullOrWhiteSpace(_configuration.GetConnectionString("ConnectionString")))
		{
			return RedirectToPage("/Index");
		}

		DescribeSettingsStatus();

		SettingsAlreadySaved = !IsBlocked && _settingsStore.SetupAccessToken is null &&
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

		DescribeSettingsStatus();
		if (IsBlocked)
		{
			// No form is offered in this state; a forged POST changes nothing.
			ErrorMessage = StatusTitle;
			return Page();
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

		// The Master-bootstrap form is a second [BindProperty] on this page. Its [Required] rules fire on
		// every POST of this form (its fields are not posted), which used to reject every submission with
		// the generic message. This handler owns only the "Input." fields.
		foreach (var key in ModelState.Keys.Where(key => key.Length > 0
			&& !key.StartsWith(nameof(Input) + ".", StringComparison.Ordinal)).ToList())
		{
			ModelState.Remove(key);
		}

		var providedToken = Encoding.UTF8.GetBytes(Input.SetupToken ?? string.Empty);
		var expectedToken = Encoding.UTF8.GetBytes(expectedTokenValue);
		if (providedToken.Length != expectedToken.Length ||
			!CryptographicOperations.FixedTimeEquals(providedToken, expectedToken))
		{
			ModelState.AddModelError(nameof(Input) + "." + nameof(Input.SetupToken),
				"کد راه‌اندازی معتبر نیست. فایل کد راه‌اندازی را روی همان سرور بررسی کنید.");
		}

		if (RequiresReplaceConfirmation && !Input.ConfirmReplace)
		{
			ModelState.AddModelError(
				nameof(Input) + "." + nameof(Input.ConfirmReplace),
				"فایل تنظیمات قبلی قابل استفاده نیست. برای جایگزینی (با نسخه پشتیبان خودکار) تیک تأیید جایگزینی را بزنید.");
		}

		var request = new DatabaseSetupRequest
		{
			Server = Input.Server,
			Database = Input.Database,
			AuthenticationMode = Input.AuthenticationMode,
			UserName = Input.UserName,
			Password = Input.Password,
			TrustServerCertificate = Input.TrustServerCertificate,
			CreateDatabaseIfMissing = Input.CreateDatabaseIfMissing,
			ContinuePartialMigrations = Input.ContinuePartialMigrations
		};

		// Even when ModelState is already invalid (e.g. the token), run the service validation so
		// that every independent field error is shown at once, not only the first.
		var fieldErrors = DatabaseConnectionSetupService.Validate(request);
		foreach (var (field, messages) in fieldErrors)
		{
			var key = nameof(Input) + "." + field;
			ModelState.Remove(key);
			foreach (var message in messages)
			{
				ModelState.AddModelError(key, message);
			}
		}

		if (!ModelState.IsValid)
		{
			ClearSecrets();
			var fieldErrorCount = ModelState.Values.Sum(entry => entry.Errors.Count);
			if (fieldErrorCount == 0)
			{
				ModelState.AddModelError(string.Empty, "اطلاعات فرم را بررسی کنید.");
			}
			else
			{
				ErrorMessage = $"{fieldErrorCount} مورد در فرم نیاز به اصلاح دارد؛ پیام هر مورد کنار همان فیلد آمده است.";
			}

			return Page();
		}

		var result = await _setupService.ConfigureAsync(request, cancellationToken);

		ClearSecrets();
		StageOutcomes = result.Stages;
		Warnings = result.Warnings;
		if (!result.Succeeded)
		{
			ErrorMessage = result.ErrorMessage ?? "راه‌اندازی پایگاه داده ناموفق بود.";
			FailureRemedy = result.Remedy;
			TrackingId = result.TrackingId;
			FailureCode = result.ErrorCode;
			FailedStage = result.FailedStage;
			return Page();
		}

		SettingsAlreadySaved = true;
		SuccessMessage = result.MigrationsApplied
			? "اتصال تأیید شد و دیتابیس با Migrationهای پروژه آماده شد. برای بارگذاری تنظیمات جدید، برنامه را متوقف و دوباره اجرا کنید."
			: "اتصال تأیید و تنظیمات به‌صورت رمزنگاری‌شده ذخیره شد. دیتابیس فرانو از قبل کامل بود و تغییری در آن داده نشد. برای بارگذاری تنظیمات جدید، برنامه را متوقف و دوباره اجرا کنید.";

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

	/// <summary>Never echo the posted password or setup token back into the page.</summary>
	private void ClearSecrets()
	{
		Input.Password = null;
		Input.SetupToken = string.Empty;

		// Keep the entries (they may carry a validation message) but drop the posted secrets from them.
		foreach (var name in new[] { nameof(Input.Password), nameof(Input.SetupToken) })
		{
			if (ModelState.TryGetValue(nameof(Input) + "." + name, out var entry))
			{
				entry.RawValue = null;
				entry.AttemptedValue = null;
			}
		}
	}

	private void DescribeSettingsStatus()
	{
		(StatusTitle, StatusRemedy) = SettingsStatus switch
		{
			DatabaseSettingsStatus.AccessDenied => (
				"فایل یا پوشه تنظیمات اتصال وجود دارد اما برنامه به آن دسترسی ندارد.",
				"دسترسی خواندن/نوشتن حساب اجرای برنامه را به پوشه تنظیمات بدهید یا مسیر را با Farano__Settings__Directory تغییر دهید و برنامه را دوباره اجرا کنید."),
			DatabaseSettingsStatus.UnsupportedVersion => (
				"فایل تنظیمات با نسخه جدیدتری از فرانو ساخته شده است.",
				"برنامه را به نسخه جدیدتر ارتقا دهید؛ فایل تغییر داده نشد."),
			DatabaseSettingsStatus.Corrupt => (
				"فایل تنظیمات اتصال خراب است و خوانده نشد.",
				"فایل پشتیبان در پوشه backups را بررسی کنید یا اتصال را دوباره بسازید؛ فایل خراب پیش از جایگزینی نسخه پشتیبان می‌شود."),
			DatabaseSettingsStatus.Undecryptable => (
				"فایل تنظیمات اتصال با کلید این کاربر/دستگاه رمزگشایی نشد.",
				"برنامه را با همان حساب قبلی اجرا کنید؛ در غیر این صورت اتصال را دوباره بسازید (فایل قبلی پشتیبان می‌شود)."),
			DatabaseSettingsStatus.LegacyUnreadable => (
				"تنظیمات نسخه قبلی برنامه پیدا شد اما خوانده یا رمزگشایی نشد.",
				"با همان حساب ویندوز قبلی اجرا کنید یا اتصال را دوباره بسازید؛ فایل قدیمی هرگز تغییر نمی‌کند."),
			_ => (null, null)
		};
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

		// Field rules live in DatabaseConnectionSetupService.Validate (one source of truth, all errors
		// at once). UserName and Password are nullable on purpose: a non-nullable reference type gets an
		// implicit [Required] from MVC, which made Windows authentication fail.
		public string Server { get; set; } = string.Empty;

		public string Database { get; set; } = FaranoSettingsNames.Name;

		public string AuthenticationMode { get; set; } = "Windows";

		public string? UserName { get; set; }

		public string? Password { get; set; }

		public bool TrustServerCertificate { get; set; }
		public bool CreateDatabaseIfMissing { get; set; } = true;

		/// <summary>Apply the missing migrations to a Farano database that is behind.</summary>
		public bool ContinuePartialMigrations { get; set; }

		/// <summary>Confirms replacing an existing but unusable settings file (it is backed up).</summary>
		public bool ConfirmReplace { get; set; }
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
