namespace SmartShopPOS.Domain.Identity;

public sealed class PurchaseOrderNumberSequence
{
    private PurchaseOrderNumberSequence()
    {
    }

    public Guid OrganizationId { get; private set; }
    public long LastNumber { get; private set; }
    public Organization Organization { get; private set; } = null!;
}
