using SmartShopPOS.Domain;
using SmartShopPOS.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Infrastructure.Persistence;

namespace SmartShopPOS.UnitTests;

public class IdentityDomainTests
{
    [Fact]
    public void Organization_NormalizesCodeAndInitializesUtcTimestamps()
    {
        var organization = new Organization("Nairobi Store", "Nairobi-Store");

        Assert.Equal("nairobi-store", organization.Code);
        Assert.True(organization.IsActive);
        Assert.Equal(TimeSpan.Zero, organization.CreatedAt.Offset);
        Assert.Equal(organization.CreatedAt, organization.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a slug")]
    [InlineData("-invalid")]
    [InlineData("invalid-")]
    public void Organization_RejectsInvalidCode(string code)
    {
        Assert.Throws<DomainException>(() => new Organization("Store", code));
    }

    [Fact]
    public void User_RequiresOrganizationAndStoresNormalizedEmailAndHash()
    {
        var organizationId = Guid.NewGuid();
        var user = new User(organizationId, " Alice@Example.com ", "Alice", "encoded-hash");

        Assert.Equal(organizationId, user.OrganizationId);
        Assert.Equal("ALICE@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("encoded-hash", user.PasswordHash);
        Assert.True(user.IsActive);
        Assert.Throws<DomainException>(() => new User(Guid.Empty, "a@example.com", "Alice", "encoded-hash"));
        Assert.Null(typeof(User).GetProperty("Password"));
    }

    [Fact]
    public void Role_RequiresOrganization()
    {
        Assert.Throws<DomainException>(() => new Role(Guid.Empty, "Cashier"));

        var organizationId = Guid.NewGuid();
        var role = new Role(organizationId, "Cashier");

        Assert.Equal(organizationId, role.OrganizationId);
    }

    [Fact]
    public void Permission_NormalizesAndValidatesMachineKey()
    {
        var permission = new Permission("Sales.Create", "Create sales");

        Assert.Equal("sales.create", permission.Key);
        Assert.Throws<DomainException>(() => new Permission("not valid", "Invalid"));
    }

    [Fact]
    public void PersistenceModel_EnforcesTenantScopedUniquenessAndAssignments()
    {
        var options = new DbContextOptionsBuilder<SmartShopPosDbContext>()
            .UseNpgsql("Host=localhost;Database=smartshoppos")
            .Options;
        using var dbContext = new SmartShopPosDbContext(options);
        var model = dbContext.Model;

        Assert.Contains(model.FindEntityType(typeof(Organization))!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(Organization.Code));
        Assert.Contains(model.FindEntityType(typeof(User))!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(User.OrganizationId), nameof(User.NormalizedEmail)]));
        Assert.Contains(model.FindEntityType(typeof(Role))!.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(Role.OrganizationId), nameof(Role.NormalizedName)]));

        var userRoleForeignKeys = model.FindEntityType(typeof(UserRole))!.GetForeignKeys();
        Assert.Contains(userRoleForeignKeys, foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(User) &&
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(UserRole.OrganizationId), nameof(UserRole.UserId)]));
        Assert.Contains(userRoleForeignKeys, foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(Role) &&
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(UserRole.OrganizationId), nameof(UserRole.RoleId)]));

        Assert.All(model.FindEntityType(typeof(Organization))!.GetReferencingForeignKeys(), foreignKey =>
            Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }
}
