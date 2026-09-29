# ADR-007: Product Pricing Foundation

- Status: Accepted
- Date: 2026-09-29

## Context

The product catalog is now stable and organization-scoped, but the system still needs a proper pricing model. Pricing must be time-aware because products can have historical cost and selling price changes without mixing those semantics into the master product record itself. If we do not isolate pricing history, we risk creating a brittle design that couples product master data to operational pricing rules, inventory, or accounting.

## Decision

We will implement product pricing as a separate, organization-scoped history model built around product price periods. Each price record includes cost price, selling price, effective-from date, and optional effective-to date. The model resolves the effective price at a point in time and restricts overlapping active periods for each product. The service layer will validate organization scope, active-product status, and date validity before creating a new price record.

## Consequences

### Positive

- Clear separation between product identity, product master data, and pricing history.
- Strong support for time-based lookup and future sales or accounting flows.
- Easier auditing of price changes and better compatibility with future discount logic.
- Compliance with the project rule to keep pricing independent from inventory, purchasing, and accounting side effects.

### Negative

- Pricing is intentionally narrow and does not include branch pricing, discounts, promotions, or tax integration.
- Additional pricing rules will be developed in future slices with their own validation and API contracts.

## Follow-up

Future features can build on this pricing foundation for sales pricing rules, discount engines, inventory valuation, and branch-specific pricing policy without reintroducing these concerns into the product catalog or core accounting model.
