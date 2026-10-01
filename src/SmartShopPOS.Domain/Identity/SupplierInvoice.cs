using System.Text;

namespace SmartShopPOS.Domain.Identity;

public enum SupplierInvoiceStatus { Draft = 1, Posted = 2, Cancelled = 3 }

public sealed class SupplierInvoice : Entity
{
    private SupplierInvoice() { }

    public SupplierInvoice(Guid organizationId, Guid supplierId, Guid? purchaseOrderId, string invoiceNumber,
        string normalizedInvoiceNumber, string internalNumber, DateOnly invoiceDate, DateOnly? dueDate,
        decimal documentNetAmount, decimal documentVatAmount, decimal otherTaxAmount, decimal documentGrossAmount,
        string? notes, Guid createdByUserId)
    {
        if (organizationId == Guid.Empty || supplierId == Guid.Empty || createdByUserId == Guid.Empty || purchaseOrderId == Guid.Empty)
            throw new DomainException("A supplier invoice requires valid organization, supplier, and creator identifiers.");
        OrganizationId = organizationId;
        SupplierId = supplierId;
        PurchaseOrderId = purchaseOrderId;
        InvoiceNumber = Preserve(invoiceNumber, 120, nameof(invoiceNumber));
        NormalizedInvoiceNumber = Required(normalizedInvoiceNumber, 120, nameof(normalizedInvoiceNumber));
        InternalNumber = Required(internalNumber, 32, nameof(internalNumber));
        if (invoiceDate == default || dueDate == DateOnly.MinValue || !ValidMoney(documentNetAmount) || !ValidMoney(documentVatAmount) || !ValidMoney(otherTaxAmount) || !ValidMoney(documentGrossAmount))
            throw new DomainException("Invoice date and non-negative document taxes are required.");
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        SupplierDocumentNetAmount = documentNetAmount;
        SupplierDocumentVatAmount = documentVatAmount;
        OtherTaxAmount = otherTaxAmount;
        SupplierDocumentGrossAmount = documentGrossAmount;
        Notes = Optional(notes, 1000);
        Status = SupplierInvoiceStatus.Draft;
        CreatedByUserId = createdByUserId;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public Guid OrganizationId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public string InternalNumber { get; private set; } = string.Empty;
    public string InvoiceNumber { get; private set; } = string.Empty;
    public string NormalizedInvoiceNumber { get; private set; } = string.Empty;
    public DateOnly InvoiceDate { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public decimal OtherTaxAmount { get; private set; }
    public decimal SupplierDocumentNetAmount { get; private set; }
    public decimal SupplierDocumentVatAmount { get; private set; }
    public decimal SupplierDocumentGrossAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal GrossAmount { get; private set; }
    public SupplierInvoiceStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? PostedByUserId { get; private set; }
    public Supplier Supplier { get; private set; } = null!;
    public PurchaseOrder? PurchaseOrder { get; private set; }
    public ICollection<SupplierInvoiceLine> Lines { get; private set; } = new List<SupplierInvoiceLine>();

    public void UpdateDraft(string invoiceNumber, string normalizedInvoiceNumber, DateOnly invoiceDate,
        DateOnly? dueDate, decimal documentNetAmount, decimal documentVatAmount, decimal otherTaxAmount,
        decimal documentGrossAmount, string? notes)
    {
        EnsureDraft();
        if (invoiceDate == default || dueDate == DateOnly.MinValue || !ValidMoney(documentNetAmount) || !ValidMoney(documentVatAmount) || !ValidMoney(otherTaxAmount) || !ValidMoney(documentGrossAmount))
            throw new DomainException("Invoice date and non-negative document taxes are required.");
        InvoiceNumber = Preserve(invoiceNumber, 120, nameof(invoiceNumber));
        NormalizedInvoiceNumber = Required(normalizedInvoiceNumber, 120, nameof(normalizedInvoiceNumber));
        InvoiceDate = invoiceDate;
        DueDate = dueDate;
        SupplierDocumentNetAmount = documentNetAmount;
        SupplierDocumentVatAmount = documentVatAmount;
        OtherTaxAmount = otherTaxAmount;
        SupplierDocumentGrossAmount = documentGrossAmount;
        Notes = Optional(notes, 1000);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetTotals(decimal net, decimal tax)
    {
        EnsureDraft();
        if (net < 0m || tax < 0m || !FitsMoney(net) || !FitsMoney(tax) || !FitsMoney(net + tax + OtherTaxAmount))
            throw new DomainException("Invoice totals must be non-negative and fit numeric(18,2).");
        NetAmount = net;
        TaxAmount = tax;
        GrossAmount = net + tax + OtherTaxAmount;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Post(Guid userId)
    {
        EnsureDraft();
        if (userId == Guid.Empty) throw new DomainException("A posting user is required.");
        Status = SupplierInvoiceStatus.Posted;
        PostedByUserId = userId;
        PostedAt = DateTimeOffset.UtcNow;
        UpdatedAt = PostedAt.Value;
    }

    public void Cancel()
    {
        EnsureDraft();
        Status = SupplierInvoiceStatus.Cancelled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void EnsureDraft()
    {
        if (Status != SupplierInvoiceStatus.Draft) throw new DomainException("Only draft supplier invoices can be changed.");
    }

    private static string Required(string? value, int max, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > max) throw new DomainException($"{field} is required and must not exceed {max} characters.");
        return normalized;
    }
    private static string Preserve(string? value, int max, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new DomainException($"{field} is required and must not exceed {max} characters.");
        return value;
    }
    private static string? Optional(string? value, int max)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > max) throw new DomainException($"Value must not exceed {max} characters.");
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
    private static bool FitsMoney(decimal value) => value == decimal.Round(value, 2) && value < 10_000_000_000_000_000m;
    private static bool ValidMoney(decimal value) => value >= 0m && FitsMoney(value);
}

public sealed class SupplierInvoiceLine : Entity
{
    private SupplierInvoiceLine() { }
    public SupplierInvoiceLine(Guid organizationId, Guid supplierInvoiceId, Guid? purchaseOrderId, Guid? purchaseOrderLineId,
        string description, decimal quantity, decimal unitPrice, decimal taxAmount, Guid? taxCategoryId = null)
    {
        if (organizationId == Guid.Empty || supplierInvoiceId == Guid.Empty || purchaseOrderLineId == Guid.Empty || taxCategoryId == Guid.Empty)
            throw new DomainException("A supplier invoice line requires valid identifiers.");
        if ((purchaseOrderId is null) != (purchaseOrderLineId is null))
            throw new DomainException("Purchase order and order line references must both be present or both be absent.");
        OrganizationId = organizationId;
        SupplierInvoiceId = supplierInvoiceId;
        PurchaseOrderId = purchaseOrderId;
        PurchaseOrderLineId = purchaseOrderLineId;
        TaxCategoryId = taxCategoryId;
        CreatedAt = UpdatedAt = DateTimeOffset.UtcNow;
        SetValues(description, quantity, unitPrice, taxAmount);
    }
    public Guid OrganizationId { get; private set; }
    public Guid SupplierInvoiceId { get; private set; }
    public Guid? PurchaseOrderId { get; private set; }
    public Guid? PurchaseOrderLineId { get; private set; }
    public Guid? TaxCategoryId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal GrossAmount => NetAmount + TaxAmount;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public void Update(Guid? purchaseOrderId, Guid? purchaseOrderLineId, Guid? taxCategoryId, string description, decimal quantity, decimal unitPrice, decimal taxAmount)
    {
        if (purchaseOrderLineId == Guid.Empty || taxCategoryId == Guid.Empty) throw new DomainException("Line references must be valid.");
        if ((purchaseOrderId is null) != (purchaseOrderLineId is null)) throw new DomainException("Purchase order and order line references must both be present or both be absent.");
        PurchaseOrderId = purchaseOrderId; PurchaseOrderLineId = purchaseOrderLineId; TaxCategoryId = taxCategoryId;
        SetValues(description, quantity, unitPrice, taxAmount); UpdatedAt = DateTimeOffset.UtcNow;
    }
    private void SetValues(string description, decimal quantity, decimal unitPrice, decimal taxAmount)
    {
        Description = description?.Trim() ?? string.Empty;
        if (Description.Length is 0 or > 300) throw new DomainException("Line description is required and must not exceed 300 characters.");
        if (quantity <= 0m || quantity != decimal.Round(quantity, 4) || quantity >= 100_000_000_000_000m) throw new DomainException("Quantity must be positive and fit numeric(18,4).");
        if (unitPrice < 0m || unitPrice != decimal.Round(unitPrice, 4) || unitPrice >= 100_000_000_000_000m) throw new DomainException("Unit price must be non-negative and fit numeric(18,4).");
        if (taxAmount < 0m || taxAmount != decimal.Round(taxAmount, 2) || taxAmount >= 10_000_000_000_000_000m) throw new DomainException("Tax amount must be non-negative and fit numeric(18,2).");
        Quantity = quantity; UnitPrice = unitPrice; NetAmount = decimal.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero); TaxAmount = taxAmount;
        if (NetAmount + TaxAmount >= 10_000_000_000_000_000m) throw new DomainException("Line gross amount exceeds numeric(18,2).");
    }
}

public sealed class SupplierInvoiceNumberSequence
{
    private SupplierInvoiceNumberSequence() { }
    public Guid OrganizationId { get; private set; }
    public long LastNumber { get; private set; }
    public Organization Organization { get; private set; } = null!;
}

public static class SupplierInvoiceNumber
{
    public static string NormalizeComparisonValue(string value) => value.Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant();
}
