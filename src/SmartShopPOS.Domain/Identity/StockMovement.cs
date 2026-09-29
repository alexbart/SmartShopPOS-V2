namespace SmartShopPOS.Domain.Identity;

public sealed class StockMovement : Entity
{
    private StockMovement()
    {
    }

    public StockMovement(
        Guid organizationId,
        Guid branchId,
        Guid productId,
        MovementType movementType,
        decimal quantity,
        DateTimeOffset occurredAt,
        string? referenceType = null,
        Guid? referenceId = null,
        string? reason = null,
        Guid? createdByUserId = null)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A stock movement must belong to an organization.");
        }

        if (branchId == Guid.Empty)
        {
            throw new DomainException("A stock movement must belong to a branch.");
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("A stock movement must belong to a product.");
        }

        if (quantity <= 0m)
        {
            throw new DomainException("Movement quantity must be greater than zero.");
        }

        if (quantity != decimal.Round(quantity, 4) || quantity >= 100_000_000_000_000m)
        {
            throw new DomainException("Movement quantity must fit numeric(18,4).");
        }

        if (!Enum.IsDefined(movementType))
        {
            throw new DomainException("Movement type is invalid.");
        }

        if (referenceType?.Length > 100 || reason?.Length > 500)
        {
            throw new DomainException("Movement reference type or reason is too long.");
        }

        OrganizationId = organizationId;
        BranchId = branchId;
        ProductId = productId;
        MovementType = movementType;
        Quantity = quantity;
        OccurredAt = occurredAt;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        Reason = reason;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public MovementType MovementType { get; private set; }
    public decimal Quantity { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public string? ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? Reason { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;
    public Branch Branch { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    public static decimal GetDelta(MovementType movementType)
    {
        return movementType switch
        {
            MovementType.Receipt => 1m,
            MovementType.Sale => -1m,
            MovementType.AdjustmentIncrease => 1m,
            MovementType.AdjustmentDecrease => -1m,
            MovementType.OpeningBalance => 1m,
            MovementType.Return => 1m,
            _ => throw new DomainException($"Unknown movement type: {movementType}")
        };
    }
}
