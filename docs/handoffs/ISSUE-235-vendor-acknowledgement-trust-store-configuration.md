# Vendor acknowledgement trust-store configuration

The Vendor host loads customer-side acknowledgement public keys from its own deployment configuration under `ProvisioningAcknowledgementTrust:Keys`. This trust is independent of the incoming acknowledgement envelope: an envelope's `KeyId` is only a lookup hint and cannot add a trusted key.

For environment-variable deployments, use the standard double-underscore separator with the Vendor prefix:

- `FARANO_VENDOR_ProvisioningAcknowledgementTrust__Keys__0__KeyId`
- `FARANO_VENDOR_ProvisioningAcknowledgementTrust__Keys__0__PublicKeySubjectPublicKeyInfoBase64`
- `FARANO_VENDOR_ProvisioningAcknowledgementTrust__Keys__0__Revoked`

The public-key value is the Base64-encoded DER SubjectPublicKeyInfo exported for an ECDSA P-256 public key. Do not place private keys in Vendor configuration, source control, or deployment artifacts. Add further entries using index `1`, `2`, etc. Key IDs must be unique and at most 128 characters.

An empty section is intentionally valid and rejects every key lookup. Invalid or duplicate configured entries fail host startup rather than silently selecting a key. Set `Revoked=true` to disable a configured key; removing a key also rejects it, but explicit revocation is preferable for auditable operations.

This configuration wires the trust-store primitive into dependency injection; it does **not** enable an HTTP acknowledgement endpoint or confirm provisioning requests. Do not mark a request confirmed until the endpoint verifies the signed acknowledgement, binds it to the original request and commits an idempotent state transition plus audit record. Production key lifecycle should be backed by approved deployment controls; this configuration alone is not a KMS/HSM.
