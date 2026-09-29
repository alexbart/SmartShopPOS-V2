namespace SmartShopPOS.Domain.Identity;

public sealed class ProductPrice : Entity
{
    private ProductPrice()
    {
    }

    public ProductPrice(
        Guid organizationId,
        Guid productId,
        decimal costPrice,
        decimal sellingPrice,
        DateTimeOffset effectiveFrom)
    {
        if (organizationId == Guid.Empty)
        {
            throw new DomainException("A product price must belong to an organization.");
        }

        if (productId == Guid.Empty)
        {
            throw new DomainException("A product price must belong to a product.");
        }

        OrganizationId = organizationId;
        ProductId = productId;
        CostPrice = ValidateMoney(costPrice, nameof(costPrice));
        SellingPrice = ValidateMoney(sellingPrice, nameof(sellingPrice));
        EffectiveFrom = ToUtc(effectiveFrom, nameof(effectiveFrom));
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid OrganizationId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal CostPrice { get; private set; }
    public decimal SellingPrice { get; private set; }
    public DateTimeOffset EffectiveFrom { get; private set; }
    public DateTimeOffset? EffectiveTo { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public Organization Organization { get; private set; } = null!;
    public Product Product { get; private set; } = null!;

    public bool IsEffectiveAt(DateTimeOffset timestamp)
    {
        var effectiveAt = ToUtc(timestamp, nameof(timestamp));
        return EffectiveFrom <= effectiveAt && (!EffectiveTo.HasValue || effectiveAt < EffectiveTo.Value);
    }

    public void CloseAt(DateTimeOffset effectiveTo)
    {
        var normalized = ToUtc(effectiveTo, nameof(effectiveTo));
        if (normalized <= EffectiveFrom)
        {
            throw new DomainException("A price period must end after it begins.");
        }

        if (EffectiveTo is not null)
        {
            throw new DomainException("A price period can only be closed once.");
        }

        EffectiveTo = normalized;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdatePricing(decimal costPrice, decimal sellingPrice)
    {
        CostPrice = ValidateMoney(costPrice, nameof(costPrice));
        SellingPrice = ValidateMoney(sellingPrice, nameof(sellingPrice));
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static decimal ValidateMoney(decimal value, string name)
    {
        if (value < 0m)
        {
            throw new DomainException($"{name} must be zero or greater.");
        }

        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static DateTimeOffset ToUtc(DateTimeOffset value, string name)
    {
        var normalized = value.ToUniversalTime();
        if (normalized == default)
        {
            throw new DomainException($"{name} is required.");
        }

        return normalized;
    }
}
