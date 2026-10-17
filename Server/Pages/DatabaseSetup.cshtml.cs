using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace Server.Pages;

public sealed class DatabaseSetupModel : PageModel
{
    private readonly DatabaseConnectionSettingsStore _settingsStore;
    private readonly DatabaseConnectionSetupService _setupService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSetupModel> _logger;

    public DatabaseSetupModel(
        DatabaseConnectionSettingsStore settingsStore,
        DatabaseConnectionSetupService setupService,
        IConfiguration configuration,
        ILogger<DatabaseSetupModel> logger)
    {
        _settingsStore = settingsStore;
        _setupService = setupService;
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string SetupTokenFilePath => _settingsStore.SetupTokenFilePath;
    public string? SuccessMessage { get; private set; }
    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (!string.IsNullOrWhiteSpace(_configuration.GetConnectionString("ConnectionString")))
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_configuration.GetConnectionString("ConnectionString")))
        {
            return RedirectToPage("/Index");
        }

        var providedToken = Encoding.UTF8.GetBytes(Input.SetupToken ?? string.Empty);
        var expectedToken = Encoding.UTF8.GetBytes(_settingsStore.SetupAccessToken ?? string.Empty);
        if (expectedToken.Length == 0 ||
            providedToken.Length != expectedToken.Length ||
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

        SuccessMessage = result.DatabaseCreated
            ? "اتصال تأیید شد، دیتابیس ساخته شد و Migrationهای پروژه اعمال شدند. برای بارگذاری تنظیمات جدید، برنامه را متوقف و دوباره اجرا کنید."
            : "اتصال تأیید و تنظیمات به‌صورت رمزنگاری‌شده ذخیره شد. دیتابیس از قبل وجود داشت و برای جلوگیری از تغییر ناخواسته داده‌ها، Migration خودکار روی دیتابیس موجود اجرا نشد. برنامه را مجدداً اجرا کنید و وضعیت Migrationها را بررسی کنید.";

        _logger.LogInformation("The first-run database setup succeeded. Database credentials were not logged.");
        return Page();
    }

    public sealed class InputModel
    {
        [Required(ErrorMessage = "کد راه‌اندازی الزامی است.")]
        public string SetupToken { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام سرور الزامی است.")]
        [StringLength(256, MinimumLength = 1)]
        public string Server { get; set; } = @".\SQLSERVER2025";

        [Required(ErrorMessage = "نام دیتابیس الزامی است.")]
        [StringLength(128, MinimumLength = 1)]
        [RegularExpression(@"^[^\[\]/\\.]+$", ErrorMessage = "نام دیتابیس شامل نویسه‌های غیرمجاز است.")]
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
}
