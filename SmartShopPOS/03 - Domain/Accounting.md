# Accounting

Accounting owns accounts, journal entries, posting rules, and financial auditability. The system must support double-entry bookkeeping. For a KES 1,000 cash sale, debit Cash and credit Sales Revenue; for KES 600 cost of goods sold, debit COGS and credit Inventory.

Important invariants: every posted entry balances; posted financial transactions are immutable; corrections use reversals, refunds, credit notes, or adjustments; monetary values use `decimal` and currency is explicit where appropriate.
