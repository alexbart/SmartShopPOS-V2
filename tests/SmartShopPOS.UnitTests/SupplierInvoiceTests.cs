using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.UnitTests;

public sealed class SupplierInvoiceTests
{
    private static SupplierInvoice NewInvoice() => new(Guid.NewGuid(), Guid.NewGuid(), null, "INV/2026/001",
        "INV/2026/001", "SI-000001", new DateOnly(2026, 10, 1), null, 20m, 3.2m, 0m, 23.2m, null, Guid.NewGuid());

    [Fact]
    public void InvoiceNumber_NormalizesOnlyComparisonValue()
    {
        Assert.Equal("INV/2026/001", SupplierInvoiceNumber.NormalizeComparisonValue("  INV/2026/001  "));
        Assert.Equal("INV/2026/001", SupplierInvoiceNumber.NormalizeComparisonValue("inv/2026/001"));
        Assert.NotEqual(SupplierInvoiceNumber.NormalizeComparisonValue("INV-2026-001"), SupplierInvoiceNumber.NormalizeComparisonValue("INV2026001"));
    }

    [Fact]
    public void InvoiceLine_UsesDecimalQuantitiesAndRoundsCalculatedNet()
    {
        var line = new SupplierInvoiceLine(Guid.NewGuid(), Guid.NewGuid(), null, null,
            "Sugar", 2.5m, 125.25m, 78.28m);
        Assert.Equal(313.13m, line.NetAmount);
        Assert.Equal(391.41m, line.GrossAmount);
        Assert.Equal(2.5m, line.Quantity);
    }

    [Fact]
    public void Invoice_PostMakesDocumentImmutableAndCancelOnlyAppliesToDraft()
    {
        var invoice = NewInvoice();
        invoice.SetTotals(20m, 3.2m);
        invoice.Post(Guid.NewGuid());
        Assert.Equal(SupplierInvoiceStatus.Posted, invoice.Status);
        Assert.NotNull(invoice.PostedAt);
        Assert.Throws<DomainException>(() => invoice.UpdateDraft("INV-2", "INV-2", new DateOnly(2026, 10, 1), null, 20m, 3.2m, 0m, 23.2m, null));
        Assert.Throws<DomainException>(() => invoice.Cancel());
    }

    [Fact]
    public void Invoice_RejectsInvalidDecimalAndDates()
    {
        Assert.Throws<DomainException>(() => new SupplierInvoiceLine(Guid.NewGuid(), Guid.NewGuid(), null, null,
            "Sugar", 0m, 3m, 0m));
        Assert.Throws<DomainException>(() => new SupplierInvoiceLine(Guid.NewGuid(), Guid.NewGuid(), null, null,
            "Sugar", 1m, -3m, 0m));
        Assert.Throws<DomainException>(() => new SupplierInvoice(Guid.NewGuid(), Guid.NewGuid(), null, "INV", "INV", "SI-1",
            default, null, 0m, 0m, 0m, 0m, null, Guid.NewGuid()));
    }
}
