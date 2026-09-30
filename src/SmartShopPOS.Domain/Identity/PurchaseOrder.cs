namespace SmartShopPOS.Domain.Identity;

public sealed class PurchaseOrder : Entity
{
    private PurchaseOrder()
    {
    }

    public PurchaseOrder(
        Guid organizationId,
        Guid supplierId,
        Guid branchId,
        string orderNumber,
        DateTimeOffset orderDate,
        DateTimeOffset? expectedDate,
        string? notes,
        Guid createdByUserId)
    {
        if (organizationId == Guid.Empty || supplierId == Guid.Empty || branchId == Guid.Empty || createdByUserId == Guid.Empty)
        {
            throw new DomainException("A purchase order requires organization, supplier, branch, and creator identifiers.");
        }

        OrganizationId = organizationId;
        SupplierId = supplierId;
        BranchId = branchId;
        OrderNumber = Organization.Required(orderNumber, nameof(orderNumber), 32);
        OrderDate = NormalizeRequiredDate(orderDate, nameof(orderDate));
        ExpectedDate = NormalizeOptionalDate(expectedDate, nameof(expectedDate));
        Notes = NormalizeNotes(notes);
        Status = PurchaseOrderStatus.Draft;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public Guid SupplierId { get; private set; }
    public Guid BranchId { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public PurchaseOrderStatus Status { get; private set; }
    public DateTimeOffset OrderDate { get; private set; }
    public DateTimeOffset? ExpectedDate { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    public Organization Organization { get; private set; } = null!;
    public Supplier Supplier { get; private set; } = null!;
    public Branch Branch { get; private set; } = null!;
    public User CreatedByUser { get; private set; } = null!;
    public User? UpdatedByUser { get; private set; }

    public void UpdateDraft(
        Guid supplierId,
        Guid branchId,
        DateTimeOffset orderDate,
        DateTimeOffset? expectedDate,
        string? notes,
        Guid updatedByUserId)
    {
        EnsureStatus(PurchaseOrderStatus.Draft);
        if (supplierId == Guid.Empty || branchId == Guid.Empty || updatedByUserId == Guid.Empty)
        {
            throw new DomainException("A purchase order update requires supplier, branch, and updater identifiers.");
        }

        SupplierId = supplierId;
        BranchId = branchId;
        OrderDate = NormalizeRequiredDate(orderDate, nameof(orderDate));
        ExpectedDate = NormalizeOptionalDate(expectedDate, nameof(expectedDate));
        Notes = NormalizeNotes(notes);
        RecordUpdate(updatedByUserId);
    }

    public void Submit(Guid updatedByUserId)
    {
        EnsureStatus(PurchaseOrderStatus.Draft);
        Status = PurchaseOrderStatus.Submitted;
        RecordUpdate(updatedByUserId);
    }

    public void Cancel(Guid updatedByUserId)
    {
        if (Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Submitted))
        {
            throw new DomainException("Only draft or submitted purchase orders can be cancelled.");
        }

        Status = PurchaseOrderStatus.Cancelled;
        RecordUpdate(updatedByUserId);
    }

    private void EnsureStatus(PurchaseOrderStatus expected)
    {
        if (Status != expected)
        {
            throw new DomainException($"Purchase order must be {expected} to perform this operation.");
        }
    }

    private void RecordUpdate(Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
        {
            throw new DomainException("An updating user is required.");
        }

        UpdatedByUserId = updatedByUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset NormalizeRequiredDate(DateTimeOffset value, string field)
    {
        if (value == default)
        {
            throw new DomainException($"{field} is required.");
        }

        return value.ToUniversalTime();
    }

    private static DateTimeOffset? NormalizeOptionalDate(DateTimeOffset? value, string field)
    {
        if (value is DateTimeOffset date && date == default)
        {
            throw new DomainException($"{field} cannot be an empty date.");
        }

        return value?.ToUniversalTime();
    }

    private static string? NormalizeNotes(string? value)
    {
        var notes = value?.Trim();
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        if (notes.Length > 1000)
        {
            throw new DomainException("Notes must not exceed 1000 characters.");
        }

        return notes;
    }
}
