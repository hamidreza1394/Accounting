using System.Security.Cryptography;

namespace Faranou.Licensing.Contracts;

/// <summary>
/// Provisional ECDSA P-256 / SHA-256 verifier (IEEE P1363). OD-12 not Accepted.
/// Does not select algorithm from attacker-controlled input beyond equality with fixed constant.
/// </summary>
public sealed class EcdsaP256Sha256Verifier : ILicenseSignatureVerifier
{
	public string Algorithm => LicenseAlgorithms.EcdsaP256Sha256;

	public bool TryVerify(LicenseEnvelope envelope, ReadOnlySpan<byte> publicKeySpki, out string? errorCode)
	{
		ArgumentNullException.ThrowIfNull(envelope);
		errorCode = null;

		if (!string.Equals(envelope.Algorithm, Algorithm, StringComparison.Ordinal))
		{
			errorCode = "algorithm_mismatch";
			return false;
		}

		if (publicKeySpki.Length is 0 or > LicenseInputLimits.MaxPublicKeyBytes)
		{
			errorCode = "public_key_invalid";
			return false;
		}

		if (string.IsNullOrEmpty(envelope.SignatureBase64)
		    || envelope.SignatureBase64.Length > LicenseInputLimits.MaxSignatureBase64Chars)
		{
			errorCode = "signature_invalid";
			return false;
		}

		byte[] signature;
		try
		{
			signature = Convert.FromBase64String(envelope.SignatureBase64);
		}
		catch (FormatException)
		{
			errorCode = "signature_encoding_invalid";
			return false;
		}

		if (signature.Length != 64)
		{
			errorCode = "signature_length_invalid";
			return false;
		}

		if (string.IsNullOrEmpty(envelope.PayloadJson)
		    || envelope.PayloadJson.Length > LicenseInputLimits.MaxPayloadJsonChars)
		{
			errorCode = "payload_size_invalid";
			return false;
		}

		var data = CanonicalJson.GetUtf8Bytes(envelope.PayloadJson);
		try
		{
			using var ecdsa = ECDsa.Create();
			try
			{
				ecdsa.ImportSubjectPublicKeyInfo(publicKeySpki, out _);
			}
			catch (CryptographicException)
			{
				errorCode = "public_key_import_failed";
				return false;
			}

			if (ecdsa.KeySize != 256)
			{
				errorCode = "public_key_curve_invalid";
				return false;
			}

			if (!ecdsa.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
			{
				errorCode = "signature_mismatch";
				return false;
			}

			return true;
		}
		catch (CryptographicException)
		{
			errorCode = "cryptographic_failure";
			return false;
		}
	}
}
