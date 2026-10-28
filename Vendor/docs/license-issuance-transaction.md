# Vendor license issuance transaction

## Transaction boundary

The license approval decision endpoint owns a single transaction against `VendorDbContext`. For an approved issuance, `VendorLicenseIssuanceService` performs all issuance writes in that transaction:

1. Revalidates the pending approval, distinct reviewer, approval row version, license row version, Draft status, and active installation/customer relationship.
2. Builds the next canonical payload using `LastIssuedSequence + 1`.
3. Requests a signature from the configured `ILicenseSigningProvider`; any unavailable/invalid signer result fails closed.
4. Persists the complete serialized signed envelope, key ID, algorithm, sequence, and SHA-256 hash of the exact canonical payload JSON.
5. Advances the license sequence and status and records the approval decision.
6. Appends approval and issuance events through `IVendorAuditWriter`, which participates in the ambient Vendor database transaction.
7. Commits only after all steps succeed.

The unique database index on `(LicenseId, Sequence)` and SQL Server row-version checks defend against duplicate issuance and concurrent decisions. If envelope persistence, a concurrency check, or audit append fails, the request transaction must not commit. Do not retry a signing or persistence failure by bypassing these checks.

## Signing configuration and production gate

The default `UnavailableLicenseSigningProvider` is deliberately fail-closed and contains no key material. Issuance will return a service-unavailable response until a reviewed production KMS/HSM signing adapter is registered. Test-only ephemeral ECDSA keys are permitted only in tests. Never put private signing keys in source control, CI logs, customer installations, or Vendor database rows.

## Revocation

Revocation is intentionally still unavailable. An offline client cannot learn about a database-only status change; production revocation needs a signed revocation artifact or a clearly defined online revocation protocol, including expiry, replay, and recovery semantics. Do not treat the Vendor status field alone as cryptographic revocation.

## Integration verification

Run Vendor SQL Server integration tests only against a disposable local SQL Server database using `FARANO_VENDOR_TEST_SQLSERVER`. Set `FARANOU_REQUIRE_VENDOR_SQLSERVER=1` in a dedicated integration environment to make a missing SQL Server connection fail rather than skip. These tests must never target a customer accounting database.
