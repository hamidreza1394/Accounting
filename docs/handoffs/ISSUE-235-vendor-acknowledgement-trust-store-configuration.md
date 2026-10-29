# Handoff — ISSUE-235 vendor acknowledgement trust-store configuration

- **Issue:** [#235](https://github.com/hamidreza1394/Accounting/issues/235)
- **Agent/owner:** ChatGPT GitHub workflow
- **Date:** 2026-10-29
- **Status:** Implemented in branch; CI verification pending

## Goal and acceptance criteria

- [x] Load acknowledgement verification keys from Vendor deployment configuration, independently of incoming acknowledgement envelopes.
- [x] Empty trust configuration rejects all key IDs; no private key or software signer is introduced.
- [x] Malformed key material and duplicate IDs fail closed during trust-store construction.
- [x] Keep the customer-side consumer, customer database schema, and confirmation intake endpoint outside this Vendor-only change.

## Summary

`Vendor.Host/Program.cs` binds `ProvisioningAcknowledgementTrust:Keys` and registers the immutable `ProvisioningAcknowledgementTrustStore` singleton. The store validates key IDs, Base64 DER SubjectPublicKeyInfo, and ECDSA P-256 public keys. Revoked and unknown key IDs cannot be resolved. An empty section is safe and cannot verify any acknowledgement.

## Deployment configuration

The Vendor host uses the `FARANO_VENDOR_` environment-variable prefix. For one trusted customer acknowledgement key, configure:

```text
FARANO_VENDOR_ProvisioningAcknowledgementTrust__Keys__0__KeyId=customer-ack-key-001
FARANO_VENDOR_ProvisioningAcknowledgementTrust__Keys__0__PublicKeySubjectPublicKeyInfoBase64=<deployment-supplied public SPKI Base64>
FARANO_VENDOR_ProvisioningAcknowledgementTrust__Keys__0__Revoked=false
```

The value is the public ECDSA P-256 key encoded as DER SubjectPublicKeyInfo and Base64. The placeholder must be replaced in the deployment secret/configuration system; do not put actual key material in GitHub, tests, issue comments, or build artifacts. The corresponding private key is never loaded by Vendor and must remain under the separately approved customer-side signing boundary.

## Decisions and limitations

- No acknowledgement HTTP intake route is added here. The host trust store is groundwork only and does not mark provisioning requests as confirmed.
- Customer-side nonce claiming, transactionally applying provisioning, and signed acknowledgement generation belong in a separate reviewed change touching `Server/**` and possibly customer schema.
- A KMS/HSM provider remains blocked on deployment selection in Issue #236.
- Empty trust configuration is allowed and fails closed at verification time. A malformed non-empty configuration fails host startup.

## Tests

| Command / test | Result |
|---|---|
| `dotnet test tests/Vendor.Host.Tests` | Not run in this environment; CI pending |
| `.NET CI / Vendor CI / secret scan / Semgrep` | To be reported from final branch SHA |

## Remaining risks

- Until an authenticated acknowledgement intake endpoint is implemented, Vendor must not transition provisioning requests to `Confirmed` based solely on package export or local state.
- Production key delivery, rotation, revocation and incident recovery require an approved operational process.
