# Vendor production-readiness checkpoint — 2026-10-29

## Scope and verified state

This is a readiness checkpoint, not a declaration that Vendor is production-complete.

- Vendor acknowledgement intake was merged in PR [#293](https://github.com/hamidreza1394/Accounting/pull/293).
- The Vendor provisioning history UI was updated to show the verified customer acknowledgement outcome and timestamp in PR [#294](https://github.com/hamidreza1394/Accounting/pull/294).
- The latest observed main-branch CI snapshot on 2026-10-29 reported success for Vendor CI, .NET CI, CodeQL and Semgrep. CI success proves the covered checks pass; it does not prove production deployment or customer-side provisioning is complete.
- Vendor provisioning remains separate from the customer accounting application. No customer database schema change is authorized by this checkpoint.

## Remaining work and gates

| Issue | Remaining work | Required gate |
|---|---|---|
| [#228](https://github.com/hamidreza1394/Accounting/issues/228) | Complete provisioning workflow end-to-end; the signed package and Vendor-side intake/history are not equivalent to creating the company and initial administrator in a customer installation. | Define and test a customer-side consumer, replay protection, safe one-time credential enrollment, and rollback behavior. |
| [#235](https://github.com/hamidreza1394/Accounting/issues/235) | Customer-side acknowledgement consumer and the complete onboarding contract. | Resolve fiscal-period and parent-company semantics, define a one-time credential enrollment flow, and approve any customer schema migration plus rollback plan before changing `Server/**` or customer schema. |
| [#236](https://github.com/hamidreza1394/Accounting/issues/236) | Production signing provider and operational key lifecycle. | Product/operations must select a real KMS/HSM provider and approve workload identity, least privilege, key custody, rotation, recovery, and emergency revocation. Keep production signing fail-closed until configured. |
| [#107](https://github.com/hamidreza1394/Accounting/issues/107) | Independent audit checkpoint storage. | Approve a destination independent of the Vendor primary database, credentials/identity, retention and immutability controls, and recovery procedure. A second table or file on the same failure domain is not independent. |
| [#214](https://github.com/hamidreza1394/Accounting/issues/214) | Offline licensing/revocation production readiness. | Complete the customer-side signed revocation verifier with persistent anti-rollback state; exercise backup/restore and upgrade/rollback; validate threat model and operational runbooks; close the KMS/HSM and independent audit gates above. |

## Security invariants

1. Never place a plaintext bootstrap password, reusable bootstrap secret, customer connection string, customer password hash, or production private signing key in a Vendor package, repository, log, or CI artifact.
2. Do not trust a key identifier supplied by a package without resolving it against independently configured trust.
3. Signed acknowledgements must be bound to the original request and installation context; exact retries may be idempotent, while conflicting terminal responses must not overwrite an accepted result.
4. Production signing must fail closed if the approved signer or trust configuration is absent. Do not silently fall back to a software private key in production.
5. Do not modify the customer database schema or `Server/**` as part of Vendor-only work without a separately reviewed PR with migration, rollback, and customer-impact tests.
6. A green CI run is not a substitute for a real KMS/HSM integration test, deployment rehearsal, restore test, or manual installation QA.

## Recommended execution order

1. Finish the provisioning contract and onboarding decisions without persisting bootstrap credentials.
2. Implement the customer-side consumer in a separate, narrowly scoped PR after the data and credential-enrollment semantics are approved. Add tests for invalid signature, expired package, replay, cross-installation binding, idempotent retries, and rollback.
3. Select and configure the production signing provider; add key rotation/recovery exercises without committing secrets.
4. Configure and verify independent audit storage and its recovery path.
5. Add persistent anti-rollback handling for offline revocation and execute upgrade, backup/restore, and offline installation scenarios.
6. Close the issues only when their acceptance criteria and operational evidence are present.

## Status

**Not production-complete.** The latest observed CI snapshot is green, but end-to-end customer provisioning, production key custody, independent audit storage, offline revocation enforcement, and operational rehearsal remain outstanding. Decisions that affect customer schema or production infrastructure must not be guessed by implementation agents.
