# ADR-006: Organization-Scoped Product Catalog Foundation

- Status: Accepted
- Date: 2026-09-29

## Context

The platform needs a reliable, organization-owned master-data catalog before it can support procurement, inventory, sales, and tax flows. The project also needs strict separation between organization-wide master data and branch-specific operational context. The product catalog cannot accidentally absorb branch, terminal, price, or stock semantics because those concerns belong to later modules.

## Decision

We will implement products, categories, brands, units of measure, and tax categories as organization-scoped entities. The product model will carry only organization, identifier, core descriptive data, and references to valid catalog records. Product identity is enforced by unique constraints within the organization, and records are logically deactivated rather than physically removed.

## Consequences

### Positive

- Strong tenant separation for catalog data.
- Clear isolation between master data and branch-specific operations.
- Easier future inventory, pricing, and sales features because they can build on a stable product reference model.
- Simpler database constraints and permission enforcement.

### Negative

- The catalog layer remains intentionally narrow and does not include downstream business logic.
- Inventory and pricing concerns must be implemented in a later feature slice with their own schema and APIs.

## Follow-up

Future slices should add inventory movements, stock balances, price lists, and branch availability using the product catalog as a stable reference model but without reintroducing branch-scoped master data into the core catalog.
