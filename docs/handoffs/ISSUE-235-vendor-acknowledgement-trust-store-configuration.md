# Handoff — ISSUE-235 vendor acknowledgement trust-store configuration

- **Issue:** [#235](https://github.com/hamidreza1394/Accounting/issues/235)
- **Agent/owner:** ChatGPT GitHub workflow
- **Date:** 2026-10-29
- **Status:** Configuration is implemented on `main` by PR #291; this branch retains the deployment guide and explicit scope boundaries.

## Goal and acceptance criteria

- [x] Vendor Host reads acknowledgement verification keys from deployment configuration independently of incoming acknowledgement envelopes (merged in PR #291).
- [x] Empty trust configuration rejects every key ID; no private key or software signer is introduced.
- [x] Malformed key material and duplicate IDs fail closed when the trust store is constructed.
- [x] Keep the customer-side consumer, customer database schema, and confirmation intake endpoint outside this Vendor-only change.

## Configuration already present on main

`Vendor/Vendor.Host/Program.cs` registers `ProvisioningAcknowledgementTrustStore` from the `ProvisioningAcknowledgementTrust:Keys` configuration section. The store validates key IDs, Base64 DER SubjectPublicKeyInfo, and ECDSA P-256 public keys. Revoked and unknown key IDs cannot be resolved. Empty configuration is safe and cannot verify an acknowledgement.

## Deployment configuration

The Vendor host uses the `FARANO_VENDOR_` environment-variable prefix. For one trusted customer acknowledgement key, configure:

```text
FARANO_VENDOR_ProvisioningAcknowledgementTrust__Keys__0__KeyId=customer-ack-key-001
FARANO_VENDOR_ProvisioningAcknowledgementTrust__Keys__0__PublicKeySubjectPublicKeyInfoBase64=<deployment-supplied public SPKI Base64>
FARANO_VENDOR_ProvisioningAcknowledgementTrust__Keys__0__Revoked=false
```

The value is the public ECDSA P-256 key encoded as DER SubjectPublicKeyInfo and Base64. The placeholder must be replaced in the deployment configuration system; do not put actual key material in GitHub, tests, issue comments, or build artifacts. The corresponding private key is never loaded by Vendor and must remain under the separately approved customer-side signing boundary.

## Decisions and limitations

- No acknowledgement HTTP intake route is added here. The trust store is groundwork only and does not mark provisioning requests as confirmed.
- Customer-side nonce claiming, transactionally applying provisioning, and signed acknowledgement generation belong in a separate reviewed change touching `Server/**` and possibly customer schema.
- A KMS/HSM provider remains blocked on deployment selection in Issue #236.
- Empty trust configuration rejects all acknowledgement key IDs. A malformed non-empty configuration fails when the trust store is constructed.

## Tests and verification

| Check | Result |
|---|---|
| Trust-store tests | Present in `tests/Vendor.Host.Tests/ProvisioningAcknowledgementTrustStoreTests.cs`; CI was green for PR #291 and will rerun for the consolidated PR. |
| Local `dotnet test` | Not run in this environment. |

## Remaining risks

- Until an authenticated acknowledgement intake endpoint is implemented, Vendor must not transition provisioning requests to `Confirmed` based solely on package export or local state.
- Production key delivery, rotation, revocation and incident recovery require an approved operational process.
