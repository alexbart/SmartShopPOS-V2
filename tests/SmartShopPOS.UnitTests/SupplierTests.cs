using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class SupplierTests
{
    [Fact]
    public void Supplier_NormalizesCodeAndTrimsOptionalDetails()
    {
        var supplier = new Supplier(Guid.NewGuid(), " sup-001 ", "  Acme Supplies  ",
            contactPerson: "  Alex  ", phone: " +254 700 000 000 ", email: "buyer@example.test");

        Assert.Equal("SUP-001", supplier.Code);
        Assert.Equal("Acme Supplies", supplier.Name);
        Assert.Equal("Alex", supplier.ContactPerson);
        Assert.Equal("+254 700 000 000", supplier.Phone);
        Assert.Equal("buyer@example.test", supplier.Email);
        Assert.True(supplier.IsActive);
    }

    [Fact]
    public void Supplier_RejectsMissingOrganizationCodeOrName()
    {
        Assert.Throws<DomainException>(() => new Supplier(Guid.Empty, "SUP-001", "Supplier"));
        Assert.Throws<DomainException>(() => new Supplier(Guid.NewGuid(), " ", "Supplier"));
        Assert.Throws<DomainException>(() => new Supplier(Guid.NewGuid(), "SUP-001", " "));
    }

    [Fact]
    public void Supplier_RejectsInvalidEmailAndOverlongFields()
    {
        Assert.Throws<DomainException>(() => new Supplier(Guid.NewGuid(), "SUP-001", "Supplier", email: "not-an-email"));
        Assert.Throws<DomainException>(() => new Supplier(Guid.NewGuid(), new string('x', 65), "Supplier"));
        Assert.Throws<DomainException>(() => new Supplier(Guid.NewGuid(), "SUP-001", "Supplier", phone: new string('1', 65)));
    }

    [Fact]
    public void Supplier_UpdateAndDeactivatePreserveLogicalLifecycle()
    {
        var supplier = new Supplier(Guid.NewGuid(), "SUP-001", "Supplier");
        supplier.Update("sup-002", "Updated supplier", null, null, null, null, null, null, null);
        supplier.SetActive(false);

        Assert.Equal("SUP-002", supplier.Code);
        Assert.Equal("Updated supplier", supplier.Name);
        Assert.False(supplier.IsActive);
    }
}
