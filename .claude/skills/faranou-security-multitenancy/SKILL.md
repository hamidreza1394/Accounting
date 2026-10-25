---
name: faranou-security-multitenancy
description: Use for authentication, authorization, company membership, tenant isolation, session/cookie, anti-forgery, sensitive data, audit, licensing, or cryptographic work in Faranou.
---

# Faranou Security and Multi-Tenancy

## Required discovery
Read `AGENTS.md`, `docs/SECURITY.md`, `docs/ARCHITECTURE.md`, `docs/DECISIONS.md`, `docs/TESTING.md`, the applicable licensing threat model/open decisions, and the relevant implementation/tests. Verify current code and current ADRs first.

## Non-negotiable invariants
- Authorization is enforced server-side; hiding UI is not authorization.
- Company access follows the existing `CompanyAccessService` and active database membership. Never trust a client-provided company/TenantId or a claim alone.
- Preserve same-origin Cookie authentication and anti-forgery protections for state-changing requests. Never store authentication tokens in localStorage.
- Apply least privilege, deny-by-default, secure cookie settings, safe redirects, rate limiting where appropriate, input validation, safe output encoding, and non-sensitive structured logging.
- Do not log passwords, tokens, signing material, private customer data, or production connection strings.
- Treat secrets committed previously as compromised and recommend rotation; never create or commit real production secrets.
- Do not add an EF Core global query filter or change the tenancy model merely by habit; assess D-013 and existing access paths first.
- Licensing, customer accounts, installations, accounting companies, and company groups are distinct concepts where current ADRs say so. Do not collapse them.

## Threat review
Trace read, create, update, delete, export, import, background work, raw SQL, administrative operations, and cross-company access. Check object-level authorization, IDOR, over-posting, race conditions, auditability, session revocation, CSRF, XSS, SQL injection, secret leakage, and denial-of-service where relevant.

For each security-relevant change, add negative tests proving unauthorized/cross-tenant requests fail and authorized flows still work. Use integration tests for middleware, cookies, anti-forgery and real authorization boundaries. Verify SQL Server behavior when persistence semantics matter.

## Vendor/licensing controls
Follow accepted decisions on offline-first customer operation, signing-key separation, rotation, MFA, maker-checker, and audit requirements. Do not implement cryptography or select a production KMS based only on generic preference. Require a reviewed threat model, exact payload/canonicalization contract, key lifecycle, recovery and rotation plan, and cross-platform tests before production claims.

## Reporting
Separate findings by severity and evidence. Mark each as confirmed, suspected, or unverified. Never call the application secure merely because a checklist exists or a scanner is green. High-impact fixes require approval before implementation unless explicitly authorized.
