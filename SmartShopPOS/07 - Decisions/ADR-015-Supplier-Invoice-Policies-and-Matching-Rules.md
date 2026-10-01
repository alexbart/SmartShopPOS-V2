# ADR-015: Supplier Invoice Policies and Matching Rules

## Status

Accepted for MVP domain policy; implementation-specific tax and approval configuration remains to be validated.

## Context

ADR-014 established separate supplier invoice, AP, payment, eTIMS, and accounting responsibilities. Schema implementation needs explicit policies for non-PO invoices, matching variances, receipt allocation, supplier invoice number duplicates, tax capture, and currency. These policies must preserve auditability without turning supplier invoicing into the accounting system.

## Decisions

### Non-PO invoices

The MVP supports both PO-backed and non-PO supplier invoices, explicitly identified as separate invoice modes. PO-backed lines reference PO lines and participate in matching. Non-PO invoices carry no fabricated PO/receipt relationship and require a controlled expenditure classification plus approval before posting. Their final account mapping belongs to the future accounting domain.

### PO, receipt, and invoice matching

Allow invoice creation and editing as Draft even when quantities or prices mismatch. Matching compares cumulative posted invoice quantity per PO line with ordered quantity and cumulative received quantity. Draft invoices do not count toward invoiced quantity. Partial and multiple invoices per PO line are supported.

- An invoiced quantity at or below both received and ordered quantities passes quantity matching.
- An invoiced quantity above received but not above ordered is a variance and requires explicit authorized, audited approval before posting.
- An invoiced quantity above ordered cannot post against the current PO. It requires a formal PO amendment capability; until such an amendment exists, the invoice remains unposted.
- Unit-cost, tax, and supplier-document total variances also require explicit approval under configured rules before posting.
- No automatic numerical or percentage tolerance is defined. A Draft mismatch is visible and cannot be silently corrected by changing the PO or receipt.

Exact approval permissions, audit fields, and any future tolerance values must be specified before implementation. There is no implied bypass based on user role names.

### Receipt allocation

The MVP does not persist SupplierInvoiceLine-to-GoodsReceiptLine allocations. Matching compares cumulative quantities at the PO-line level. The system must not imply that aggregate matching proves which receipt supplied the invoiced units. Explicit allocation and receipt-level reconciliation remain future capabilities.

### VAT and other taxes

Preserve the supplier document's stated net, VAT, other-tax, and gross amounts as source-document values. Independently calculate expected line and tax totals from configured classifications/rules and expose differences for review. Do not silently substitute calculated values for supplier-stated amounts. Posting requires document-total/tax variances to satisfy explicit approval rules.

This decision defines data ownership, not Kenyan tax rates or legal treatment. Tax categories, rates/effective dates, rounding, recoverability, required tax evidence, and applicable Kenya/eTIMS requirements must be validated against authoritative guidance before implementation. Core purchasing records must not contain eTIMS-provider-specific lifecycle or transport behavior.

### Supplier invoice number

Keep the exact supplier-provided invoice number unchanged for audit. Derive a separate duplicate-comparison value by Unicode NFC normalization, trimming surrounding whitespace, and invariant case-insensitive comparison. Preserve internal spaces and punctuation; do not strip separators or prefixes. Enforce duplicate detection within `(OrganizationId, SupplierId)` using the normalized value. The internal SmartShopPOS invoice number remains separate, immutable, and organization-scoped.

### Currency

The initial invoice/AP release supports KES only. Amounts remain decimal and invoice currency is an explicit domain boundary so a future release can add currencies deliberately. No exchange rate, converted functional amount, or foreign-currency settlement is implied in the MVP. Multi-currency support must preserve the exact posting rate, date, source, and base-currency amount once its accounting treatment is designed.

## Consequences

Non-PO invoices are allowed but require explicit classification and approval. Invoiced quantities remain independent of ordered and received quantities. Receipt-line allocations, automatic tolerance, and multi-currency posting are deferred. Supplier document tax evidence is retained while configured calculations support discrepancy review. Posting creates an AP obligation but does not directly create journal entries or manipulate account balances; accounting consumes the posted financial transaction through a separately designed, reliable integration boundary. This ADR introduces no code, schema, migration, payment flow, tax engine, eTIMS integration, or accounting tables.
