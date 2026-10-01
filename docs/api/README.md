# API Documentation

Use this area for API conventions, resource contracts, error behavior, authentication requirements, and integration guidance. OpenAPI generated from implementation is the contract source of truth once the API exists.

## Purchase Order Receiving Summary

`GET /api/v1/purchase-orders/{purchaseOrderId}/receiving-summary` returns the PO number and persisted document status, derived receiving state, aggregate ordered/received/remaining quantities, and line-level receiving progress. `ReceivingState` is `NotReceived`, `PartiallyReceived`, or `FullyReceived`; an order with no lines is `NotReceived`. A cancelled PO retains its historical summary.

The summary is read-only and derives all quantities from `PurchaseOrderLine.Quantity` and `GoodsReceiptLine.QuantityReceived`. It does not change PO status or write inventory data. Access requires authentication, `purchase_orders.lines.view`, and the same selected branch operational context and branch assignment used by the PO-lines API. Cross-organization orders return 404.
