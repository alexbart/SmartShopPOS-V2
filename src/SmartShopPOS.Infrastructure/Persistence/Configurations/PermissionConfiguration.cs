using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    private static readonly PermissionSeed[] InitialPermissions =
    [
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af01"), "users.view", "View users"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af02"), "users.manage", "Manage users"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af03"), "roles.view", "View roles"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af04"), "roles.manage", "Manage roles"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af05"), "sales.create", "Create sales"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af06"), "reports.view", "View reports"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af07"), "branches.view", "View branches"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af08"), "branches.create", "Create branches"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af09"), "branches.update", "Update branches"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0a"), "branches.deactivate", "Deactivate branches"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0b"), "terminals.view", "View terminals"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0c"), "terminals.create", "Create terminals"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0d"), "terminals.update", "Update terminals"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0e"), "terminals.deactivate", "Deactivate terminals"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af0f"), "user_branch_assignments.view", "View user branch assignments"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af10"), "user_branch_assignments.create", "Assign users to branches"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af11"), "user_branch_assignments.deactivate", "Deactivate user branch assignments"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af12"), "branch_context.select", "Select operational branch context"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af13"), "products.view", "View products"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af14"), "products.create", "Create products"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af15"), "products.update", "Update products"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af16"), "products.deactivate", "Deactivate products"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af17"), "categories.view", "View categories"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af18"), "categories.create", "Create categories"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af19"), "categories.update", "Update categories"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af1a"), "categories.deactivate", "Deactivate categories"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af1b"), "brands.view", "View brands"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af1c"), "brands.create", "Create brands"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af1d"), "brands.update", "Update brands"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af1e"), "brands.deactivate", "Deactivate brands"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af1f"), "units.view", "View units"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af20"), "units.create", "Create units"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af21"), "units.update", "Update units"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af22"), "units.deactivate", "Deactivate units"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af23"), "tax_categories.view", "View tax categories"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af24"), "tax_categories.create", "Create tax categories"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af25"), "tax_categories.update", "Update tax categories"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af26"), "tax_categories.deactivate", "Deactivate tax categories"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af27"), "products.prices.view", "View product prices"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af28"), "products.prices.create", "Create product prices"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af29"), "inventory.view", "View inventory balances"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af2a"), "inventory.opening_balance", "Create inventory opening balances"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af2b"), "inventory.adjust", "Adjust inventory"),
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af2c"), "inventory.movements.view", "View stock movements")
    ];

    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(permission => permission.Id);
        builder.Property(permission => permission.Key).HasMaxLength(100).IsRequired();
        builder.Property(permission => permission.Name).HasMaxLength(120).IsRequired();
        builder.Property(permission => permission.Description).HasMaxLength(500);
        builder.HasIndex(permission => permission.Key).IsUnique().HasDatabaseName("ux_permissions_key");
        builder.HasData(InitialPermissions.Select(permission => new
        {
            Id = permission.Id,
            Key = permission.Key,
            Name = permission.Name,
            Description = (string?)null
        }));
    }

    private sealed record PermissionSeed(Guid Id, string Key, string Name);
}
