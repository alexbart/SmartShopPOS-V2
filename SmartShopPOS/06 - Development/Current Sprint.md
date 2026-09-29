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

## Milestone 7 - Product Pricing Foundation

**Current task:** Product pricing history and effective pricing resolution

**Status:** Product pricing is implemented as an organization-scoped, time-aware history model with effective-date validation, a versioned pricing API, and PostgreSQL-backed persistence tests.

**Current implementation:** Product prices are stored separately from the source product master records and include cost/selling values, effective-from timestamps, and optional effective-to timestamps. The service layer resolves the current or point-in-time price, enforces tenant ownership, and prevents overlapping active periods for the same product. Permissions are distinct from catalog management and are validated before create and view operations.

**Deferred:** Branch-specific pricing, discounts, promotions, tax integration, sales ledger posting, and inventory valuation.

## Milestone 8 - Branch Inventory and Append-Only Stock Ledger

**Current task:** Inventory and stock ledger foundation (Prompt 009)

**Status:** Inventory balances, movement history, selected-branch APIs, database migration, and unit/PostgreSQL integration coverage are implemented.

**Current implementation:** Products remain organization-scoped. Balances and movements are branch-scoped with tenant-safe composite foreign keys, uniqueness and quantity constraints, append-only movement APIs, movement direction mapping, opening balance and reasoned adjustment operations, and transactional updates. The branch context and inventory-specific permissions are required for all operations.

**Deferred:** Purchasing, sales posting, stock transfers, stocktakes, inventory valuation, costing, unit conversion, and movement reversal workflows.

## Milestone 9 - Supplier Foundation

**Current task:** Organization-scoped supplier master data (Prompt 010)

**Status:** Supplier entity, tenant-scoped CRUD service/API, permissions, migration, Swagger coverage, and domain/PostgreSQL tests are implemented.

**Current implementation:** Supplier codes are normalized and unique within an organization. Suppliers are logically deactivated. Supplier operations use authenticated organization context only and do not require a selected branch.

**Deferred:** Purchase orders and lines, goods receipts, supplier balances, accounts payable, payments, and receipt stock movements.
