using System.Text;

namespace Infrastructure.DatabaseSetup;

/// <summary>
/// <c>dotnet Server.dll --create-master-credential</c>: produces a new infrastructure (Master)
/// credential for an existing deployment, e.g. to replace a legacy SHA-256 value.
/// </summary>
/// <remarks>
/// The password is read from the console without echo (or from standard input when it is
/// redirected), checked by the same rules as the first-run setup
/// (<see cref="InfrastructureMasterBootstrap" />), hashed with the current algorithm and only
/// the hash is written to standard output, together with the configuration entries to set.
/// Nothing is logged, written to disk or stored by the application; the raw password is
/// never printed. The web host is not started.
/// </remarks>
public static class MasterCredentialCommand
{
	public const string Argument = "--create-master-credential";

	/// <param name="readLine">Reads a visible line (the username).</param>
	/// <param name="readSecret">Reads a line without echoing it (the password).</param>
	public static int Run(Func<string?> readLine, Func<string?> readSecret, TextWriter output, TextWriter error)
	{
		output.Write("Infrastructure (Master) username: ");
		var username = readLine();

		output.Write("New password (not shown): ");
		var password = readSecret();
		output.WriteLine();

		output.Write("Repeat the password (not shown): ");
		var confirmation = readSecret();
		output.WriteLine();

		var result = new InfrastructureMasterBootstrap(new Security.PasswordHashService())
			.Prepare(username, password, confirmation);

		if (!result.Succeeded)
		{
			error.WriteLine(result.ErrorMessage);
			return 1;
		}

		output.WriteLine();
		output.WriteLine("Store these values in the secret store / environment of the deployment (not in Git):");
		output.WriteLine(result.ProductionEnvironmentVariables);
		output.WriteLine();
		output.WriteLine("Development (User Secrets):");
		output.WriteLine(result.DevelopmentCommands);
		output.WriteLine();
		output.WriteLine("After the new value is active and the sign-in was verified, set");
		output.WriteLine("ApplicationSettings__AllowLegacyMasterPasswordHash=false to refuse the legacy format.");
		return 0;
	}

	/// <summary>Runs the command on the process console.</summary>
	public static int RunOnConsole()
	{
		return Run(Console.ReadLine, ReadConsoleSecret, Console.Out, Console.Error);
	}

	private static string? ReadConsoleSecret()
	{
		if (Console.IsInputRedirected)
		{
			return Console.ReadLine();
		}

		var builder = new StringBuilder();

		while (true)
		{
			var key = Console.ReadKey(intercept: true);

			if (key.Key == ConsoleKey.Enter)
			{
				return builder.ToString();
			}

			if (key.Key == ConsoleKey.Backspace)
			{
				if (builder.Length > 0)
				{
					builder.Length--;
				}

				continue;
			}

			if (!char.IsControl(key.KeyChar))
			{
				builder.Append(key.KeyChar);
			}
		}
	}
}
