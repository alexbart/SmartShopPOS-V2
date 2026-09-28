using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organizations");
        builder.HasKey(organization => organization.Id);
        builder.Property(organization => organization.Name).HasMaxLength(200).IsRequired();
        builder.Property(organization => organization.Code).HasMaxLength(63).IsRequired();
        builder.Property(organization => organization.IsActive).IsRequired();
        builder.Property(organization => organization.CreatedAt).IsRequired();
        builder.Property(organization => organization.UpdatedAt).IsRequired();
        builder.HasIndex(organization => organization.Code).IsUnique().HasDatabaseName("ux_organizations_code");
    }
}