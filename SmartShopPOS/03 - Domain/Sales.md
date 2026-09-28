# Sales

Sales/POS owns the cashier workflow, sale lines, pricing decisions, completion, voids, and refunds. A completed sale must coordinate payment, inventory movement, accounting entries, and audit information consistently.

Important invariants: sale totals must reconcile to lines and payments; completed records are not silently rewritten; refunds and voids use controlled, auditable operations; a sale cannot leave related financial or stock effects partially committed.

See [[Inventory]], [[Accounting]], and [[Payments]].
