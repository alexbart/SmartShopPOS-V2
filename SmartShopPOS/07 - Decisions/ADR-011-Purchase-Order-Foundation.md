# ADR-011: Purchase Order Foundation

## Status

Accepted

## Context

Supplier master data exists, but purchasing must be introduced in controlled slices before goods receipt can create inventory stock movements. Purchase orders need durable tenant-safe references without prematurely implementing purchasing lines, receipts, or accounting.

## Decision

Purchase orders are organization-owned header documents. Each references a supplier and destination branch through organization-composite foreign keys. The API derives organization identity from the authenticated user and requires selected destination branch context, active assignment, and operation permission for writes. Order numbers are generated using a PostgreSQL per-organization counter in the same transaction as order creation, with a unique organization/number constraint.

Orders follow Draft -> Submitted and Draft/Submitted -> Cancelled transitions. Only Draft orders may be edited. Cancellation is logical and preserves the document. No delete route is exposed.

## Consequences

The foundation provides traceable supplier/branch purchasing headers and generated numbering safe under concurrent creation. It does not contain lines, totals, receiving, inventory changes, supplier balances, accounts payable, payments, or accounting. A future submission workflow must validate its complete lines, and a future goods receipt workflow will be the purchasing path that creates inventory `Receipt` movements.
