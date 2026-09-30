# Purchasing Foundation

## Purchase Orders

Purchase orders are organization-owned purchasing documents that reference an organization-scoped supplier and a destination branch in that same organization. Tenant identity comes from the authenticated server-side user; supplier and branch identifiers in requests are selectors and are validated against that tenant. Create, update, submit, and cancel require the destination branch to be selected in the server-side operational context and require an active branch assignment and operation permission.

Each organization receives a sequentially generated `PO-000001` style number. A PostgreSQL counter row and unique organization/number constraint protect generation under concurrent requests. Orders have three states: Draft, Submitted, and Cancelled. Draft orders can be edited and submitted; Draft and Submitted orders can be cancelled. There is no delete endpoint.

This foundation stores the header only. It intentionally has no lines, totals, goods receipts, supplier balances, or inventory effects. Future purchase order lines must be complete and valid before submission. Goods receipt workflows will later reference submitted orders and create `Receipt` stock movements through the inventory service; purchase orders themselves never change stock.

## Deferred

Purchase order lines and quantities, goods receipts, purchasing valuation, accounts payable, payment workflows, accounting, and inventory movements are later slices.
