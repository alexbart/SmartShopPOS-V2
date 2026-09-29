# ADR-010: Organization-Scoped Supplier Foundation

**Status: Accepted**

## Context

Supplier records are reusable master data for future purchasing. A supplier can serve multiple branches of one organization, so tying suppliers to a selected branch would duplicate ownership and complicate later purchasing documents.

## Decision

Store suppliers under the authenticated organization, with codes unique within that organization. Supplier CRUD authorization uses global permission keys and does not require branch context. Deactivation is logical so future purchasing references can retain supplier identity.

This slice includes only supplier master data. Purchase orders, goods receipts, supplier balances, payments, and inventory movements are deferred.

## Consequences

Future purchasing documents can reference organization-owned suppliers while recording their own operational details. Receiving stock will be implemented in a later slice and will be the purchasing operation that creates receipt movements.
