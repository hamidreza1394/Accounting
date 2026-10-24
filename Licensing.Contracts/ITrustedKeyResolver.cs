namespace Faranou.Licensing.Contracts;

/// <summary>
/// Resolves kid → public key. Unknown or retired keys must not verify.
/// </summary>
public interface ITrustedKeyResolver
{
	bool TryResolve(string kid, out TrustedPublicKey? key);
}
