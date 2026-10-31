# Trade module

See docs/TRADE_MODULE.md and ADR D-061.

The module is intentionally isolated from Accounting Domain and persistence. Its current foundation contains domain primitives, application ports and a Trade-owned EF Core context. Host registration, authorization adapters, entitlement enforcement, API/UI and migrations are intentionally staged for follow-up work.
