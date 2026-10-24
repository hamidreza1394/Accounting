using System.Security.Cryptography;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Provisional verifier for OD-12 proposal (ECDSA P-256 / SHA-256, IEEE P1363 signature).
/// Not an Accepted product decision until the owner confirms OD-12.
/// </summary>
public sealed class EcdsaP256Sha256Verifier : ILicenseSignatureVerifier
{
	public string Algorithm => LicenseAlgorithms.EcdsaP256Sha256;

	public bool TryVerify(LicenseEnvelope envelope, ReadOnlySpan<byte> publicKeySpkiOrRaw, out string? error)
	{
		ArgumentNullException.ThrowIfNull(envelope);
		error = null;

		if (!string.Equals(envelope.Algorithm, Algorithm, StringComparison.Ordinal))
		{
			error = "algorithm mismatch";
			return false;
		}

		byte[] signature;
		try
		{
			signature = Convert.FromBase64String(envelope.SignatureBase64);
		}
		catch (FormatException)
		{
			error = "invalid signature encoding";
			return false;
		}

		if (signature.Length != 64)
		{
			error = "ECDSA P-256 IEEE P1363 signature must be 64 bytes";
			return false;
		}

		var data = CanonicalJson.GetUtf8Bytes(envelope.PayloadJson);
		try
		{
			using var ecdsa = ECDsa.Create();
			ImportPublicKey(ecdsa, publicKeySpkiOrRaw);
			if (!ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
			{
				error = "signature verification failed";
				return false;
			}
			return true;
		}
		catch (CryptographicException ex)
		{
			error = "cryptographic error: " + ex.Message;
			return false;
		}
	}

	private static void ImportPublicKey(ECDsa ecdsa, ReadOnlySpan<byte> key)
	{
		try
		{
			ecdsa.ImportSubjectPublicKeyInfo(key, out _);
			return;
		}
		catch (CryptographicException)
		{
			// fall through
		}

		if (key.Length == 65 && key[0] == 0x04)
		{
			var parameters = new ECParameters
			{
				Curve = ECCurve.NamedCurves.nistP256,
				Q = new ECPoint
				{
					X = key.Slice(1, 32).ToArray(),
					Y = key.Slice(33, 32).ToArray()
				}
			};
			ecdsa.ImportParameters(parameters);
			return;
		}

		throw new CryptographicException("Unrecognized public key format");
	}
}
