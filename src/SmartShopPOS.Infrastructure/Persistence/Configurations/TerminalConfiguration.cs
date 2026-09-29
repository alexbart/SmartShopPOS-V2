using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class TerminalConfiguration : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> builder)
    {
        builder.ToTable("terminals");
        builder.HasKey(terminal => terminal.Id);
        builder.Property(terminal => terminal.Code).HasMaxLength(32).IsRequired();
        builder.Property(terminal => terminal.Name).HasMaxLength(120).IsRequired();
        builder.Property(terminal => terminal.IsActive).IsRequired();
        builder.Property(terminal => terminal.CreatedAt).IsRequired();
        builder.Property(terminal => terminal.UpdatedAt).IsRequired();
        builder.HasIndex(terminal => new { terminal.BranchId, terminal.Code })
            .IsUnique()
            .HasDatabaseName("ux_terminals_branch_id_code");
        builder.HasIndex(terminal => terminal.BranchId).HasDatabaseName("ix_terminals_branch_id");
        builder.HasIndex(terminal => terminal.OrganizationId).HasDatabaseName("ix_terminals_organization_id");
        builder.HasOne(terminal => terminal.Branch)
            .WithMany(branch => branch.Terminals)
            .HasForeignKey(terminal => new { terminal.OrganizationId, terminal.BranchId })
            .HasPrincipalKey(branch => new { branch.OrganizationId, branch.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}