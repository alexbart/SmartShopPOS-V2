# Current Sprint

## Milestone 5 - User/Branch Access and Operational Context

**Current task:** User/branch access and operational branch context

**Status:** Explicit user-to-branch assignments, tenant-safe persistence, permission-scoped APIs, and per-session operational context are implemented with PostgreSQL-backed tests.

**Current implementation:** Assignment history is retained through deactivation and duplicate active assignments are database-constrained. `/api/me/branches` lists active assigned branches. `/api/me/branch-context` selects/reads a branch stored on the server-side authentication session; every context operation validates identity, tenant, active user/branch/assignment, and permission.

**Deferred:** Branch-scoped user access in future operational APIs, branch/terminal update and deactivation endpoints, audit wiring, terminal context, hardware integration, and operational workstation behavior.

## Milestone 6 - Organization-Scoped Product Catalog Foundation

**Current task:** Product catalog and master-data foundation

**Status:** Organization-owned master data for products, categories, brands, units of measure, and tax categories is implemented with EF Core migrations, API endpoints, permission checks, and unit tests.

**Current implementation:** The catalog remains organization-scoped and deliberately excludes branch, terminal, pricing, quantity, and stock fields. The schema enforces unique identities by organization and preserves logical deactivation via `IsActive`. The API is versioned under `/api/v1` and keeps tax rate logic abstracted away from eTIMS provider logic.

**Deferred:** Inventory movements, stock balances, pricing matrices, sales and purchase flows, accounting, M-Pesa, and eTIMS integrations.
