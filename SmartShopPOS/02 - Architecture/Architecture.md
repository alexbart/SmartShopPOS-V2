# Architecture

## Intended Application Architecture

```text
React + TypeScript Web POS
          |
          v
ASP.NET Core API
          |
          v
Application / Domain / Infrastructure
          |
          v
PostgreSQL
```

The browser is the POS and management experience. The API owns application access and coordinates domain and infrastructure concerns. PostgreSQL is the primary production database, with constraints and transactions contributing to correctness.

Organizations own branches, and branches own registered terminals. Branch management derives organization scope from the authenticated user. A terminal also stores `OrganizationId` so a composite foreign key can enforce that its branch belongs to the same organization.

Products remain organization-level master data. Inventory is branch-level operational data keyed by organization, branch, and product. `InventoryBalance` is the current quantity projection; append-only `StockMovement` records explain each change. Composite foreign keys enforce tenant consistency, while transactional stock operations keep movement and balance updates atomic. See [[Inventory & Stock Ledger]].

Suppliers are organization-scoped master data independent of branch context. Supplier management does not create purchasing documents or inventory movements; later purchasing slices will reference suppliers from purchase orders and goods receipts. See [[Suppliers]].

Purchase orders are organization-owned documents that reference a same-organization supplier and destination branch through composite foreign keys. Order numbers come from a per-organization transactional counter and a unique database constraint. Draft, Submitted, and Cancelled are persisted document-lifecycle states. PO receiving state (NotReceived, PartiallyReceived, FullyReceived) is derived from PO-line quantities and receipt lines and is never persisted or used to transition PO status. Submitted PO lines are immutable through the ordinary API; cancellation after receipt stops future receiving but preserves receipts and stock movements. The PO-lines API returns ordered, received, remaining, and fully-received values using database-side aggregation without another mutable projection. No Closed state or automatic completion transition is defined. See [[Purchasing]] and [[ADR-013-Purchase-Order-Lifecycle-and-Receiving-State]].

Supplier invoices are organization-scoped documents separate from PO commitments and goods receipts: invoices represent supplier claims, POs represent commitments, and receipts represent physical arrivals. Draft PO-backed and non-PO invoices are implemented with sequential internal numbers, exact supplier number preservation plus normalized duplicate comparison, source-document tax snapshots, and calculated decimal totals. Database tenant and PO-line constraints protect relationships. Posting makes the document immutable and establishes the AP boundary without journals. PO-backed posting is currently strict: quantities cannot exceed received/ordered, unit price and document totals must match, and no mismatch approval flow exists. Non-PO posting remains blocked until expenditure classification and approval policy are implemented. No invoice posting creates stock movements, inventory values, payments, or accounting entries. See [[ADR-014-Supplier-Invoice-and-Accounts-Payable-Domain-Boundary]] and [[ADR-015-Supplier-Invoice-Policies-and-Matching-Rules]].

## Operational Authorization Scopes

These scopes are separate and must not substitute for each other:

```text
Organization scope
    != Branch operational scope
    != Terminal scope
```

Organization scope is derived from the authenticated identity and limits tenant-owned records. Branch-operational operations additionally require the active server-side branch context and `IBranchAccessService.CanOperateInBranchAsync` with the operation's permission; database queries and mutations must include both organization and branch. Client-supplied IDs are selectors only. Terminal context is a later layer and must not be inferred from branch access.

## API Versioning

Public HTTP APIs use major URI versions such as `/api/v1/...`, with version-specific OpenAPI documents. Released versions keep backward-compatible contracts; breaking changes require a new major version. The existing unversioned routes predate this policy and are pre-stable: introduce the versioned routes before external clients depend on them, and document any compatibility period if old routes must remain available.

## Eventual Hardware Architecture

```text
Browser POS
    |
    v
Local Windows Hardware Agent
    |
    +--> Receipt Printer
    +--> Cash Drawer
    +--> Local Devices
```

The web application and hardware layer are separated so browser and core business logic do not depend on printer protocols, Windows queues, or device-specific behavior. The local agent will provide a controlled boundary for locally attached devices while the web platform remains deployable and manageable as a web application.

See [[Receipt Printer]], [[Cash Drawer]], and [[Barcode Scanner]].
# Goods receipt boundary

Goods receipts are posted operational events. The PO determines the receiving branch and product lines; branch context/access is checked against that destination. One serializable transaction writes the receipt, receipt lines, stock movements, and current InventoryBalance projection. PostgreSQL composite tenant keys protect receipt-to-order and receipt-line-to-PO-line relationships. Database-backed idempotency protects inventory from duplicate POST retries. Stock movements explain changes; balances remain the current projection. Goods receipt does not perform AP, accounting, valuation, tax, or payment work.
