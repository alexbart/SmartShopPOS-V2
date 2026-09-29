# ADR-009: Inventory and Append-Only Stock Ledger

**Status: Accepted**

## Context

Products are shared organization master data, while stock is operationally specific to a branch. A directly editable quantity cannot explain why stock changed and is vulnerable to concurrent writes.

## Decision

Keep products organization-scoped and store stock in `InventoryBalance`, unique by organization, branch, and product. Treat this as the current operational projection. Record each change in an append-only `StockMovement` ledger; movement quantities are positive and movement type defines direction. Stock-changing service operations write the movement and update the balance atomically, require server-side selected branch context and the relevant permission, and reject negative stock. Opening balance establishes at most one baseline for a branch/product; adjustments require a reason.

Composite foreign keys enforce branch and product ownership by the same organization. No inventory quantity is stored on Product, and this slice does not calculate stock valuation.

## Consequences

Queries use the balance projection and movement history explains changes. New purchasing and sales workflows must post their stock effects through movements in the same business consistency boundary. Corrections will need compensating movements; movement update/delete operations are not exposed. Transfers, stocktaking, valuation, costing, unit conversion, and reversal workflows remain future decisions.
