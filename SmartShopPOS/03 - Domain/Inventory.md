# Inventory & Stock Ledger

## Product and inventory ownership

Products are organization-level catalog records. Inventory is operational data for a specific organization, branch, and product. Products are not duplicated per branch and do not store a quantity.

`InventoryBalance` is the current operational projection, unique for an organization/branch/product. Its quantity is decimal, supports fractional units, and cannot be negative. It uses the product's configured unit of measure; conversion is outside this slice.

`StockMovement` is the append-only explanation for quantity changes. Its quantity is always positive; movement type determines direction:

| Movement | Direction |
| --- | ---: |
| Receipt | + |
| Sale | - |
| AdjustmentIncrease | + |
| AdjustmentDecrease | - |
| OpeningBalance | + |
| Return | + |

The service is the single source for movement deltas. The ledger is the audit trail; `InventoryBalance` is its current operational projection. Corrections require a future compensating/reversal movement rather than editing or deleting history.

## Access and consistency

Every operation derives organization from the authenticated identity and branch from the server-side selected operational context. Branch access and the operation-specific permission are checked through `IBranchAccessService`; tenant and branch scopes are also applied to database queries. Composite foreign keys enforce that the branch and product belong to the same organization.

Opening balance is limited to one baseline per organization/branch/product. Adjustments require a reason. New movements require active products and branches; historical records remain queryable after deactivation. Stock changes insert the movement and update/create the balance in one database transaction. Adjustments use PostgreSQL serializable isolation; a serialization conflict returns 409 so the caller can reload and retry. Negative stock is rejected. No client may directly set a balance.

The PostgreSQL integration suite covers opening-balance uniqueness, atomic rejection of an insufficient decrease, fractional balances, tenant constraints, and branch uniqueness. A separately orchestrated simultaneous-decrease stress test remains future test work; serializable conflict handling is implemented without relying on that nondeterministic test.

Purchasing and sales workflows will later create receipt/sale movements. Transfers, stocktakes, valuation, costing, unit conversion, and reversal workflows are future slices. No inventory valuation is calculated here; `CostPrice` is not used for stock valuation.
# Goods Receipt Integration

GoodsReceipt is the purchasing-to-inventory boundary. A committed receipt appends one positive `Receipt` StockMovement per receipt line and transactionally updates the corresponding branch InventoryBalance using the central movement direction mapping. The generic ledger reference is `GoodsReceipt` plus its receipt identifier; the receipt line links quantities to the originating PO line. Partial receiving is supported and over-receiving is rejected while the purchase order row is locked. Receipt lines, not a duplicated PO received-quantity field, are the authoritative history. Cost valuation and accounting remain outside inventory.
