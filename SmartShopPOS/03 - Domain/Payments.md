# Payments

Payments owns payment methods, payment state, allocation to sales, provider references, reconciliation, and failure handling. M-Pesa is an integration behind a provider abstraction, not logic embedded throughout Sales.

Important invariants: payment effects are auditable; external callbacks are idempotent; provider references and callback identities are uniquely constrained where appropriate; duplicate callbacks never create duplicate financial effects.
