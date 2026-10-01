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

## Milestone 10 - Purchase Order Foundation

**Current task:** Organization-owned purchase order headers (Prompt 011)

**Status:** Draft, submitted, and cancelled header lifecycle, generated organization-scoped order numbers, tenant-safe supplier/branch references, permission-checked API, migration, Swagger operations, and focused domain tests are implemented.

**Current implementation:** Create, draft update, submit, and cancel require access to the order's destination branch in the selected server-side context. A transactional PostgreSQL per-organization counter and unique constraint generate order numbers. Orders are tenant-scoped and retained after cancellation.

**Deferred:** Purchase order lines, totals, goods receipts, inventory receipt movements, supplier balances, accounts payable, purchasing valuation, payments, and accounting.

## Milestone 11 - Purchase Order Lines

**Current task:** Draft purchase order product lines (Prompt 012)

**Status:** Organization-safe PO lines, draft-only create/update/delete operations, product-specific uniqueness and quantity/cost constraints, versioned API, permissions, migration, Swagger coverage, and unit/PostgreSQL tests are implemented.

**Current implementation:** Lines reference Product master data without snapshots, use decimal quantities and document-specific unit costs, and calculate line totals for responses only. Parent PO rows are locked for line mutations so line writes serialize with order status transitions. Line changes have no inventory effects.

**Deferred:** Goods receipt reversals, purchase taxes/discounts, valuation, supplier balances, accounts payable, payments, and accounting.

## Milestone 12 - Goods Receipt Foundation

**Current task:** Posted receipts and inventory integration (Prompt 013)

**Status:** Goods receipt records, tenant-aware persistence, versioned APIs, permission seeding, and stock ledger integration are implemented.

**Current implementation:** Receipts can post partial quantities against Submitted POs. Receipt creation locks the PO in a serializable transaction, validates cumulative received amounts, creates receipt lines and positive Receipt movements, and updates InventoryBalance in the same transaction. Organization-scoped persisted idempotency binds a request hash to its receipt. Receipt lines are the received-quantity source; PO status and unit costs remain unchanged.

**Deferred:** Receipt reversal, purchasing valuation, supplier invoices/AP, accounting, payments, tax, and sales workflows.

## Milestone 13 - Purchase Order Receiving Progress

**Current task:** Derived PO-line receiving progress (Prompt 015)

**Status:** The PO-lines API returns ordered, received, remaining, and fully-received values derived from persisted receipt lines.

**Current implementation:** Receipt quantities are aggregated in the database with the PO-line query. No receiving progress is persisted or synchronized separately; newly posted receipts are reflected automatically. PO lifecycle status remains unchanged by receiving progress.

**Deferred:** Receiving dashboards, automated PO status transitions, and other purchasing lifecycle changes.
