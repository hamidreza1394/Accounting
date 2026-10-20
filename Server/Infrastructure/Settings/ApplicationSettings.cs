namespace Infrastructure.Settings
{
	public class ApplicationSettings : object
	{
		public static readonly string KeyName = nameof(ApplicationSettings);

		public ApplicationSettings() : base()
		{
			IconSettings =
				new IconSettings();

			ToastSettings =
				new ToastSettings();

			CultureSettings =
				new CultureSettings();

			FileManagerSettings =
				new FileManagerSettings();

			TablesDefaultSettings =
				new TablesDefaultSettings();
		}

		// **********
		public string? Version { get; set; }
		// **********

		// **********
		public string? MasterUsername { get; set; }
		// **********

		public string? MasterPassword { get; set; }
		// **********

		// **********
		/// <summary>
		/// Whether a <see cref="MasterPassword" /> in the legacy Base64 SHA-256 format is still
		/// accepted. Default true for compatibility; set false once the credential was replaced
		/// with one produced by <c>--create-master-credential</c> (see docs/SECURITY.md).
		/// </summary>
		public bool AllowLegacyMasterPasswordHash { get; set; } = true;
		// **********

		// **********
		public string[]? ActivationKeys { get; set; }
		// **********

		// **********
		public IconSettings IconSettings { get; set; }
		// **********

		// **********
		public ToastSettings ToastSettings { get; set; }
		// **********

		// **********
		public CultureSettings CultureSettings { get; set; }
		// **********

		// **********
		public FileManagerSettings FileManagerSettings { get; set; }
		// **********

		// **********
		public TablesDefaultSettings TablesDefaultSettings { get; set; }
		// **********
	}
}
