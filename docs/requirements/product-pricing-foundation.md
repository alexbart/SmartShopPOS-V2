# Product Pricing Foundation

## Scope

This feature covers the organization-scoped pricing history for a product and the logic needed to resolve the effective price at a point in time. It includes price records with cost price, selling price, and valid date ranges. It deliberately excludes discounts, promotions, pricing by branch, pricing by terminal, tax calculation rules beyond tax-category references, inventory valuation, purchase ledger posting, and sales accounting.

## Requirements

- Each pricing record belongs to the authenticated organization and to a valid product in that organization.
- A product can have a historical series of price periods, each with a start timestamp and an optional end timestamp.
- Only one active pricing period may be effectively open for a product at a time.
- Price periods are validated to prevent negative currency values and overlapping date ranges.
- The system can resolve the current price or the price effective at a requested timestamp.
- The API is versioned under `/api/v1` and follows the same authorization and error conventions as the catalog APIs.
- Product pricing permissions are distinct from core product management permissions.
- Product pricing is kept separate from inventory, purchasing, sales, and accounting logic.

## Accepted Boundaries

- Product pricing is a master-data foundation for future pricing and sales flows.
- Price records are not a replacement for inventory balances or stock movements.
- Price records do not carry branch or terminal context; branch-specific pricing remains a later feature slice.
- Historical pricing is intended to support auditability, point-in-time evaluation, and future sales pricing logic.

## Validation

The implementation includes domain validation tests for non-negative money, date ranges, and point-in-time effective pricing, plus PostgreSQL-backed persistence tests that verify organization scoping and date-history rules.
