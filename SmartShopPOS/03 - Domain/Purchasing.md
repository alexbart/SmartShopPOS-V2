# Purchasing Foundation

## Purchase Orders

Purchase orders are organization-owned purchasing documents that reference an organization-scoped supplier and a destination branch in that same organization. Tenant identity comes from the authenticated server-side user; supplier and branch identifiers in requests are selectors and are validated against that tenant. Create, update, submit, and cancel require the destination branch to be selected in the server-side operational context and require an active branch assignment and operation permission.

Each organization receives a sequentially generated `PO-000001` style number. A PostgreSQL counter row and unique organization/number constraint protect generation under concurrent requests. Orders have three states: Draft, Submitted, and Cancelled. Draft orders can be edited and submitted; Draft and Submitted orders can be cancelled. There is no delete endpoint.

Purchase order lines belong to a purchase order and reference organization-level Product master data. Each product may appear once per PO. Lines store decimal quantity in the Product's configured unit of measure and a document-specific `UnitCost`, separate from catalog `ProductPrice`; line total is calculated for display and is not persisted as authoritative data. Draft lines may be added, edited, or removed. Submitted and cancelled orders and their lines are immutable. Submitting an order does not require lines in this foundation; a future purchasing workflow may impose that rule.

Line operations require a line-specific permission and the same server-selected destination branch access as their parent order. The database enforces same-organization references to both PurchaseOrder and Product, positive quantity, non-negative cost, and one line per product per order. Line changes do not alter inventory balances or create stock movements. A future goods receipt workflow will be responsible for creating `Receipt` movements through the inventory service.

## Goods Receipts

A posted Goods Receipt records quantities actually received against a Submitted Purchase Order. Each organization receives a sequential `GR-000001` style number from a transactional PostgreSQL counter, with an organization/number unique constraint. Receipt destination branch and products are derived from the PO and its lines. Receipt creation requires goods-receipt permission plus active access to the PO branch in the selected operational context; the branch must be active and products must remain active. Draft and Cancelled orders cannot be received. Partial receipts are allowed, while cumulative quantities above the ordered quantity are rejected. PO status is not changed by receiving.

The receipt, lines, `StockMovement(Receipt)` entries, and `InventoryBalance` changes commit in one serializable transaction while locking the parent PO. Goods receipt lines remain the authoritative received-quantity history; PO lines do not contain a separately editable received quantity. Each movement references the GoodsReceipt and uses a positive quantity; the stock ledger's central movement mapping increases the current balance. Receipts are immutable after posting. A database-backed organization-scoped idempotency key prevents retrying the same POST from applying stock twice and rejects key reuse with a different payload.

This boundary records operational quantities only. It does not create supplier invoices, AP, accounting entries, valuation, payments, or tax records. GoodsReceipt is the only purchasing operation in this slice that creates receipt stock movements.

## Deferred

Supplier invoices, purchasing valuation, accounts payable, payment workflows, accounting, receipt reversals, taxes, and discounts are later slices.
