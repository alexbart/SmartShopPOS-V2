# Product Catalog Foundation

## Scope

This feature covers master-data for the organization-level product catalog only. It includes products, categories, brands, units of measure, and tax categories. It deliberately excludes stock on hand, pricing matrices, sales, purchases, inventory movements, accounting, and branch-specific operational context.

## Requirements

- All catalog entities are scoped to the authenticated organization.
- Product master data does not carry branch, terminal, quantity, or price properties.
- Product identity is unique within an organization by SKU and by barcode when present.
- Categories, brands, units, and tax categories are also unique within an organization.
- Records use logical deactivation via `IsActive` rather than hard deletion.
- Validation occurs in both domain entities and the database schema.
- Permissions control catalog operations with distinct view/create/update/deactivate rights.
- Public API routes are versioned under `/api/v1`.

## Accepted Boundaries

- Organization owns the master catalog.
- Branch/terminal context is validated only when a branch-scoped operation executes.
- Tax category rate is an abstract percentage used by future business logic; it is not an integration contract to eTIMS or other providers.
- Inventory behavior is intentionally deferred to a separate feature because it requires stock movements, branch availability, and role-specific business rules.

## Validation

The implementation includes domain validation tests for normalization, required identifiers, and range rules, plus PostgreSQL-backed integration tests when the `ConnectionStrings__DefaultConnection` environment variable is configured.
