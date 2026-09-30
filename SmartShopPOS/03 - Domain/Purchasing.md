# Purchasing Foundation

## Purchase Orders

Purchase orders are organization-owned purchasing documents that reference an organization-scoped supplier and a destination branch in that same organization. Tenant identity comes from the authenticated server-side user; supplier and branch identifiers in requests are selectors and are validated against that tenant. Create, update, submit, and cancel require the destination branch to be selected in the server-side operational context and require an active branch assignment and operation permission.

Each organization receives a sequentially generated `PO-000001` style number. A PostgreSQL counter row and unique organization/number constraint protect generation under concurrent requests. Orders have three states: Draft, Submitted, and Cancelled. Draft orders can be edited and submitted; Draft and Submitted orders can be cancelled. There is no delete endpoint.

Purchase order lines belong to a purchase order and reference organization-level Product master data. Each product may appear once per PO. Lines store decimal quantity in the Product's configured unit of measure and a document-specific `UnitCost`, separate from catalog `ProductPrice`; line total is calculated for display and is not persisted as authoritative data. Draft lines may be added, edited, or removed. Submitted and cancelled orders and their lines are immutable. Submitting an order does not require lines in this foundation; a future purchasing workflow may impose that rule.

Line operations require a line-specific permission and the same server-selected destination branch access as their parent order. The database enforces same-organization references to both PurchaseOrder and Product, positive quantity, non-negative cost, and one line per product per order. Line changes do not alter inventory balances or create stock movements. A future goods receipt workflow will be responsible for creating `Receipt` movements through the inventory service.

## Deferred

Goods receipts, purchasing valuation, accounts payable, payment workflows, accounting, inventory movements, taxes, and discounts are later slices.
