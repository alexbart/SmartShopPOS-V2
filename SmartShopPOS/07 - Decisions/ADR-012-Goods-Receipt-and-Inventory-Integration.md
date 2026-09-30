# ADR-012: Goods Receipt and Inventory Integration

## Status

Accepted

## Context

Purchase orders describe requested quantities, while the stock ledger records goods actually entering a branch. Mixing those events would make inventory inaccurate and unauditable. Receipt retries and concurrent partial receipts can otherwise duplicate stock or exceed the ordered amount.

## Decision

GoodsReceipt is the boundary between purchasing and inventory. Only a posted receipt for a Submitted PO may create `Receipt` stock movements. It derives branch and products from the PO and PO lines, requires selected-branch access, and uses one serializable transaction with a parent-order row lock to validate cumulative quantity, write immutable receipt records, append movements, and update balance projections. Receipt lines are the source for received-quantity totals. A persistent organization-scoped idempotency key binds the request hash to the receipt. Tenant-aware composite foreign keys enforce parent/line consistency.

## Consequences

Partial receipt is supported; over-receiving and duplicate request-line IDs are rejected. PO status remains unchanged. Receipt references in the stock ledger use `GoodsReceipt` and its ID. Receipt records are immutable and have no reversal flow in this slice. Supplier invoices, AP, accounting, tax, payment, and inventory valuation remain separate future work.
