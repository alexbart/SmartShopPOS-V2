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
        new(new Guid("dd733879-15dd-4bdd-9a1b-068cc4c5af06"), "reports.view", "View reports")
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