# Vendor Audit Checkpoint — Operations and Recovery Runbook

> **Status:** Provider-neutral runbook template. This is not evidence that a Production checkpoint sink has been selected or configured. Issue [#107](https://github.com/hamidreza1394/Accounting/issues/107) remains open until the independent sink, identity/credentials, retention/immutability, alerting and recovery procedure are exercised in the target environment.

## 1. Non-negotiable safety properties

- The audit chain in the Vendor SQL database and the independent checkpoint sink are separate failure and permission domains.
- Do not use a local file, the Vendor SQL database, or a sink whose credentials are available to the same SQL/runtime administrator as the independent checkpoint.
- The Production host must fail closed when an approved production checkpoint adapter is not registered. Checkpoint write/verification failures must not be reported as successful.
- Never delete, truncate, rewrite or silently regenerate audit rows/checkpoints to make a restored database appear consistent.
- Keep the SQL runtime principal separate from the migration/maintenance principal. Runtime access should not include UPDATE/DELETE on audit-chain tables unless a separately reviewed operation requires it.
- Do not put credentials, private keys, full sensitive payloads or customer secrets in this runbook, issues, CI logs or audit details.

## 2. Required environment-specific values

Before Production activation, record these in the approved secret/configuration system (not this file):

- Selected independent checkpoint provider and resource identifier.
- Runtime workload identity and its minimum append/read permissions.
- Separate operator/migration identity.
- Retention/immutability policy and the expected recovery point objective.
- Alert destinations and on-call owner.
- Approved backup locations, encryption/key ownership and restore test schedule.
- Provider-specific commands/API operations and expected success/failure responses.

If any required value is missing, do not mark the environment Production-ready.

## 3. Pre-deployment and routine verification

1. Confirm the Vendor host is using the approved Production configuration and that the independent store is registered; do not bypass the startup guard.
2. Run the audit-chain verifier and record the verified event count and head hash.
3. Read the latest external checkpoint and verify its schema/hash version, event count, head hash, timestamp and canonical digest.
4. Confirm the checkpoint is equal to or a valid predecessor of the verified local chain head. A checkpoint ahead of the available local chain is a restore/divergence incident, not a reason to lower the checkpoint.
5. Append a checkpoint only after full chain verification and only when the store can enforce atomic, monotonic append. Duplicate retries must be idempotent; stale/forked checkpoints must be rejected.
6. Verify alerting by using the provider's approved non-production test mechanism. Do not deliberately corrupt Production audit rows to test alerts.
7. Record the run time, software version, event count, head hash, checkpoint digest and operator identity. Do not record credentials or full event payloads.

## 4. Backup procedure

1. Take an application-consistent SQL Server backup using the approved database backup policy.
2. Preserve the independent checkpoint history using the provider's documented backup/export/immutability mechanism. A SQL backup alone is insufficient.
3. Preserve the configuration metadata needed to identify the provider, trust configuration, schema/hash versions and retention policy; keep secrets in the secret manager and out of the backup manifest.
4. Record the SQL backup identifier, backup timestamp, latest verified event count/head hash, and latest checkpoint digest.
5. Periodically perform a restore drill into an isolated environment. Do not restore over Production as a test.

## 5. Restore and recovery procedure

1. Stop or place the Vendor service into its approved maintenance mode before restoring the database. Prevent new audit writes during reconciliation.
2. Restore SQL Server to the selected recovery point in an isolated/recovery environment.
3. Verify the restored audit chain from its genesis or the documented trusted anchor; record the restored event count and head hash.
4. Read the latest checkpoint from the independent store and verify its signature/integrity (where provided), canonical digest, schema/hash version and monotonic event count.
5. Compare the restored local chain to the external checkpoint:
   - **Exact match:** proceed to application smoke tests and controlled service restart.
   - **Local chain contains the checkpoint and has a valid verified continuation:** verify the complete local chain and append a new checkpoint only after the configured policy permits it.
   - **Checkpoint is ahead of the restored database:** fail closed. Restore a newer complete backup or execute the approved incident-recovery procedure. Never lower/overwrite the checkpoint to match the older database.
   - **Hash mismatch, fork, missing parent, unknown version or unreadable checkpoint:** fail closed and escalate as a possible integrity incident.
6. Preserve both the restored database and checkpoint evidence for investigation. Do not run scripts that rewrite PreviousHash, EventHash, event count or checkpoint history.
7. Restart only after the designated operator records the reconciliation result and the verifier passes. Re-enable write traffic and confirm alerting.

## 6. Sink outage or credential failure

- Treat timeout, authentication failure, permission denial, unavailable resource or non-monotonic append as an operational failure.
- Do not fall back to a local file or the Vendor database.
- Do not claim checkpoint success, and do not silently skip the checkpoint operation.
- Follow the selected provider's recovery/credential-rotation procedure; verify the chain and latest checkpoint before retrying.
- Alert the on-call owner and preserve correlation IDs/error categories without exposing secret values.

## 7. Access control and change management

- Application runtime identity: minimum required database access; no schema migration privileges and no audit-chain mutation privileges beyond those explicitly needed by the writer.
- Migration identity: used only by controlled deployment/maintenance; not embedded in the running application.
- Checkpoint identity: separate from SQL runtime and administrator identities; only the permissions required by the chosen append/read design.
- Changes to provider, identity, retention, immutability, trust roots or recovery procedure require review, a documented change record and a successful restore drill.

## 8. Exit criteria for Issue #107

Do not close #107 until all of the following are evidenced in the target deployment:

- [ ] A real independent provider is selected and configured.
- [ ] Production startup and runtime failure paths are fail-closed.
- [ ] Separate credentials/identities and SQL permission boundaries are verified.
- [ ] Retention/immutability and alerting are enabled and tested.
- [ ] Backup and restore drill succeeds, including a deliberate older-database-versus-newer-checkpoint scenario in a test environment.
- [ ] Runbook values and provider-specific steps are completed without placing secrets in Git.
- [ ] SQL Server integration tests and all required CI checks pass on the final SHA.


## 8. SQL Server append-only runtime permissions

A reviewed starting script is available at scripts/sql/vendor-audit-runtime-permissions.sql. It is not executed automatically and must be reviewed/applied by a DBA against the Vendor database after migrations:

1. Create two distinct database users mapped to separate logins/workload identities: one for Vendor runtime and one for migrations.
2. Run the script in SQLCMD mode with VendorRuntimeUser and VendorMigrationUser variables.
3. The runtime user receives db_datareader/db_datawriter for ordinary Vendor application tables, plus membership in VendorAuditAppendOnly. That role grants SELECT/INSERT and denies UPDATE/DELETE/ALTER on dbo.VendorAuditEvents.
4. The script refuses a runtime principal in sysadmin, db_owner, db_ddladmin, db_securityadmin or db_accessadmin. Do not bypass this check.
5. The migration principal is deliberately not modified by the script. Keep its DDL permissions out of the running host's connection string.
6. Validate the resulting effective permissions with a least-privilege test login before deployment. Test that SELECT/INSERT succeeds and UPDATE/DELETE/TRUNCATE/ALTER fails on VendorAuditEvents while required application DML on other tables still works.

This is a SQL permission boundary, not an independent immutable checkpoint sink. It does not protect against sysadmin, database owner, a compromised migration identity or a privileged DBA. The independent checkpoint provider remains mandatory.


## 9. Periodic verification and alerting

- Production runs a full chain verification/checkpoint advance at startup and every configured interval (default 300 seconds).
- Configuration keys: AuditCheckpoint:IntervalSeconds (30–3600) and AuditCheckpoint:MaximumStalenessSeconds (at least two intervals, at most 86400; default 900).
- The endpoint /health/audit-checkpoint reports only Healthy/Unhealthy and a generic description. It does not expose checkpoint hashes, event details, provider endpoints or credentials.
- Configure the external monitoring system to poll /health/audit-checkpoint at least once per minute and alert on HTTP 503, Unhealthy, endpoint timeout or prolonged lack of successful verification.
- A background checkpoint failure records only the exception type, emits a Critical log event without provider message details, and leaves health Unhealthy until a subsequent full verification succeeds.
- The health endpoint is not a substitute for an alert receiver, on-call ownership or a tested notification path. Those remain deployment-specific.
