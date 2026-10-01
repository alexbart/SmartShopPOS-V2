namespace SmartShopPOS.Contracts.Purchasing;

public sealed record SupplierInvoiceCreateRequest(Guid SupplierId, Guid? PurchaseOrderId, string InvoiceNumber,
    DateOnly InvoiceDate, DateOnly? DueDate, decimal SupplierDocumentNetAmount = 0m,
    decimal SupplierDocumentVatAmount = 0m, decimal OtherTaxAmount = 0m,
    decimal SupplierDocumentGrossAmount = 0m, string? Notes = null);
public sealed record SupplierInvoiceUpdateRequest(string InvoiceNumber, DateOnly InvoiceDate, DateOnly? DueDate,
    decimal SupplierDocumentNetAmount = 0m, decimal SupplierDocumentVatAmount = 0m,
    decimal OtherTaxAmount = 0m, decimal SupplierDocumentGrossAmount = 0m, string? Notes = null);
public sealed record SupplierInvoiceLineRequest(Guid? PurchaseOrderLineId, string Description, decimal Quantity,
    decimal UnitPrice, decimal TaxAmount = 0m, Guid? TaxCategoryId = null);
public sealed record SupplierInvoiceLineResponse(Guid Id, Guid? PurchaseOrderLineId, Guid? TaxCategoryId,
    string Description, decimal Quantity, decimal UnitPrice, decimal NetAmount, decimal TaxAmount, decimal GrossAmount);
public sealed record SupplierInvoiceResponse(Guid Id, string InternalNumber, Guid SupplierId, string SupplierName,
    Guid? PurchaseOrderId, string? PurchaseOrderNumber, string InvoiceNumber, DateOnly InvoiceDate, DateOnly? DueDate,
    string Status, decimal NetAmount, decimal TaxAmount, decimal OtherTaxAmount, decimal GrossAmount,
    decimal SupplierDocumentNetAmount, decimal SupplierDocumentVatAmount, decimal SupplierDocumentOtherTaxAmount,
    decimal SupplierDocumentGrossAmount,
    string? Notes, DateTimeOffset CreatedAt, DateTimeOffset? PostedAt, IReadOnlyList<SupplierInvoiceLineResponse> Lines);
public sealed record SupplierInvoiceSummaryResponse(Guid Id, string InternalNumber, Guid SupplierId,
    string SupplierName, Guid? PurchaseOrderId, string InvoiceNumber, DateOnly InvoiceDate, string Status,
    decimal GrossAmount, DateTimeOffset CreatedAt);
