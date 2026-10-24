namespace Faranou.Licensing.Contracts;

public sealed class StaticTrustedKeyResolver : ITrustedKeyResolver
{
	private readonly Dictionary<string, TrustedPublicKey> _keys;

	public StaticTrustedKeyResolver(IEnumerable<TrustedPublicKey> keys)
	{
		ArgumentNullException.ThrowIfNull(keys);
		_keys = new Dictionary<string, TrustedPublicKey>(StringComparer.Ordinal);
		foreach (var k in keys)
		{
			ArgumentNullException.ThrowIfNull(k);
			if (!_keys.TryAdd(k.Kid, k))
				throw new ArgumentException($"Duplicate kid: {k.Kid}");
		}
	}

	public bool TryResolve(string kid, out TrustedPublicKey? key)
	{
		if (string.IsNullOrEmpty(kid) || !_keys.TryGetValue(kid, out key))
		{
			key = null;
			return false;
		}
		return true;
	}
}
