# ADR-004: Immutable Financial Transactions

**Status: Accepted**

## Context

Retail sales, payments, inventory valuation, and accounting records must remain trustworthy and auditable after posting.

## Decision

Posted financial transactions are immutable. Corrections use controlled mechanisms such as reversals, refunds, credit notes, or adjustments rather than destructive edits or silent historical rewrites.

## Consequences

Audit history and financial reconstruction remain reliable. Workflows must model correction operations explicitly, and reporting must account for original entries and their related corrections.
