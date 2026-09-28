namespace SmartShopPOS.UnitTests;

public class UnitTest1
{
    [Fact]
    public void DomainBaseTypes_ShouldCreateValidEntityIdentifier()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    private sealed class TestEntity : SmartShopPOS.Domain.Entity
    {
    }
}
