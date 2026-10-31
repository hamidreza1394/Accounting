# Trade module

See docs/TRADE_MODULE.md and ADR D-061.

The module is intentionally isolated from Accounting Domain and persistence. It reuses the Core `FiscalPeriod` and `WorkContext`; Trade does not own a parallel operational-period entity, table, or selector. Trade business records will store `TenantId` and `FiscalPeriodId` as logical references, validated by the Host. Its current foundation contains application ports and a Trade-owned EF Core context. The Host references Trade.Application and registers the CompanyAccessService/WorkContext-backed authorization adapter. Entitlement enforcement, permission catalog/assignment UI, API/UI use cases and Trade data migrations remain staged for follow-up work.
