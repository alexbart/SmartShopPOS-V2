# Inventory

Inventory owns products, stock availability, receipts, sales issues, returns, and adjustments. Stock must be traceable through movements such as purchase receipt, sale, return, and adjustment.

Important invariants: movement history remains available; current quantities may be maintained for performance; quantities and references are validated; stock changes participate in the same consistency boundary as the business operation that caused them.
