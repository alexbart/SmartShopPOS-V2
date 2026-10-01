# ADR-013: Purchase Order Lifecycle and Receiving State

## Status

Accepted

## Context

Purchase order status describes the lifecycle of the purchasing document. Receiving progress describes fulfillment against its lines. Combining both concepts in one status enum would couple document lifecycle to operational quantities and create ambiguous states, especially when a cancelled PO already has receipts.

## Decision

Keep persisted PO status as `Draft`, `Submitted`, or `Cancelled`. Keep receiving state derived as `NotReceived`, `PartiallyReceived`, or `FullyReceived` from immutable submitted line quantities and persisted GoodsReceiptLine quantities. `ReceivedQuantity`, `RemainingQuantity`, receiving state, and PO completion are not separately persisted. A fully received PO remains Submitted; receiving does not automatically transition its status.

Submitted PO line ProductId, Quantity, and UnitCost remain immutable through the ordinary API. Future corrections require an explicit amendment workflow. The existing cumulative over-receipt rule remains in force, so a fully received PO cannot receive more than ordered.

A Submitted PO may be cancelled under the existing lifecycle rule, including after partial or full receipt. Cancellation prevents further receiving and does not delete, reverse, or otherwise invalidate posted receipts or their stock movements. Receipt reversal or return behavior is a separate future workflow. Do not add a `Closed` status until its purchasing, invoice, supplier-reconciliation, and accounting semantics are defined.

## Consequences

The PO-lines API can expose fulfillment progress without maintaining a mutable projection. Historical receipts remain valid when an order is cancelled. A future amendment, closure, or reversal feature must be explicit and preserve the distinction between document lifecycle and fulfillment state. This decision requires no schema migration.
