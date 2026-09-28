# M-Pesa

M-Pesa will be integrated behind a payment-provider abstraction. The core sales domain should depend on stable payment concepts rather than provider-specific callbacks or credentials.

Planned principles:

- Validate and safely handle callbacks.
- Enforce idempotency for duplicate requests and callbacks, including database uniqueness where appropriate.
- Reconcile provider records with internal payments.
- Preserve transaction references and financial effects for auditability.

This is a planning note; current provider and regulatory details are intentionally not specified here.
