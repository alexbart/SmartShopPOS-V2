# ADR-014: Supplier Invoice and Accounts Payable Domain Boundary

## Status

Accepted domain boundaries; detailed implementation policies remain open as listed below.

## Context

Purchase orders describe what the organization committed to buy. Goods receipts describe what physically arrived and create inventory movements. Supplier invoices describe what the supplier claims is owed. Combining these records would obscure differences between ordered, received, invoiced, and paid quantities and amounts. Supplier invoicing also creates financial obligations, but SmartShopPOS has not yet defined its AP subledger or double-entry accounting domain.

## Decision

Keep SupplierInvoice separate from PurchaseOrder and GoodsReceipt. Invoice quantities and prices are independently captured and never copied as authoritative values from the PO. The intended matching model compares PO lines, receipts against those PO lines, and supplier invoice lines. It exposes variances separately from accounting posting. Partial and multiple invoices against a PO line are supported; posted invoice quantities aggregate independently per PO line, and draft invoices do not contribute to invoiced-quantity totals.

Supplier invoices have a Draft, Posted, or Cancelled document lifecycle, separate from AP settlement state. Drafts are editable and have no AP effect; only Draft invoices may be cancelled. Posting makes the invoice financially effective, creates an AP obligation, and makes the invoice and its lines immutable. Posted invoices are corrected through future credit/debit notes or explicit reversals rather than destructive edits. Outstanding, PartiallyPaid, and Paid describe AP settlement, not invoice status. Payments and allocations are separate records; a payment may eventually settle multiple invoices.

Each invoice will have an immutable organization-scoped internal document number, planned in a sequential `SI-000001` format. The supplier-provided invoice number is stored separately, retained as supplied, and unique within `(OrganizationId, SupplierId)`. Due date is optional. Currency is explicit with KES as the operational default; amounts and quantities use decimal types. Invoice totals are calculated from invoice lines and tax components, not accepted as a client-supplied grand total.

Accounts Payable is a subledger responsibility, separate from the accounting general ledger. Invoice posting will not directly update account balances or create journal rows in the supplier-invoice foundation. Once a double-entry accounting domain exists, a posted AP financial transaction will cross an explicit, reliable integration boundary into immutable journal posting. eTIMS stays behind a provider/integration boundary and does not define core invoice lifecycle. Supplier invoicing does not create stock movements or implement inventory valuation, COGS, FIFO, or weighted-average costing.

## Open Decisions Before Schema Implementation

- Whether the first invoice workflow permits non-PO invoices. PO linkage must not become optional merely through nullable foreign keys.
- Whether invoice/receipt/PO mismatches can be posted, require approval, or are constrained by tolerances. Matching and discrepancy visibility are required; policy is not yet fixed.
- Whether invoice lines allocate to individual receipt lines or match against aggregate received quantities per PO line.
- Kenya VAT/tax calculation, tax categories/rates, snapshots, and input-tax treatment.
- Case, whitespace, and normalization semantics for uniqueness while retaining the supplier number exactly as supplied.
- Foreign-currency exchange rates, conversion timing, and functional-currency values for future accounting.
- The AP open-item and payment-allocation model and its reliable handoff to journal posting.

## Consequences

Purchasing, inventory receiving, supplier claims, AP settlement, payment processing, eTIMS, and accounting remain separate responsibilities. The design introduces no schema, code, accounting tables, or tax behavior. Those implementation choices must follow the open-policy decisions and a separately designed double-entry ledger contract.
