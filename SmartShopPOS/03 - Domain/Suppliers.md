# Suppliers

Suppliers are reusable organization-level master data. They are not branch-scoped, and supplier operations do not require selected branch context. The authenticated organization is taken from server-side identity; requests cannot choose or change the owning organization.

Supplier codes are trimmed, uppercased, and unique within an organization. Suppliers retain contact, tax, and business-registration details with bounded optional fields. Email is validated when supplied. Suppliers start active and are logically deactivated; normal API deletion preserves the record. Inactive suppliers may still be updated, following the catalog lifecycle convention.

The supplier master-data API is separate from purchasing. Future purchase orders and goods receipts may reference suppliers, but this slice creates no purchasing documents, supplier balances, payments, or inventory movements.
