# Issue #504 — Server-side session lifecycle (D-069)

## Current behavior
- ASP.NET Core auth cookie uses a 30-minute sliding expiration.
- SessionValidator checks account active state and CredentialStamp on every request.
- Explicit logout records LogoutSucceeded through UserSessionAuditService, then clears auth cookie and work context.
- No independent UserSession row or SessionId claim exists; per-session listing and revocation are not supported.
- Missing logout event is not evidence of logout, expiry, or session duration.

## Proposed design — owner approval required
- Create one UserSession per successful login; use a random SessionId claim protected by the auth cookie.
- Bind sessions to installation user, not selected tenant/company.
- Validate SessionId, UserId, RevokedAtUtc and 30-minute idle window on each request; fail closed for missing/invalid/revoked/mismatched sessions.
- Preserve sliding idle behavior; do not add an absolute lifetime in v1. Update LastSeenAtUtc at most once per five minutes.
- Self-service APIs: GET /api/v1/me/sessions, POST /api/v1/me/sessions/{sessionId}/revoke and POST /api/v1/me/sessions/revoke-others.
- Password change revokes all sessions; retain CredentialStamp as defense in depth.
- Do not store raw IP or User-Agent in UserSession. Keep Infrastructure/Master account outside this table until separately decided.
- Proposed fields: Id, UserId, CreatedAtUtc, LastSeenAtUtc, RevokedAtUtc, RevokedByUserId, RevocationReasonCode, RowVersion. No TenantId.

## Decisions needed
Owner must approve idle/absolute lifetime policy, revoke-current and revoke-others behavior, retention of revoked/idle records and audit, no IP/User-Agent storage, and whether installation admins may revoke other users' sessions.

## Guardrails
D-069 is Proposed only. No migration, entity, endpoint, claim, cookie behavior, cleanup job, or audit retention change is authorized before explicit approval. No inferred logout/expiry/duration from missing events.
