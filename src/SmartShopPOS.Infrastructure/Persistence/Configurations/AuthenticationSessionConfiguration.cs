using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence.Configurations;

public sealed class AuthenticationSessionConfiguration : IEntityTypeConfiguration<AuthenticationSession>
{
    public void Configure(EntityTypeBuilder<AuthenticationSession> builder)
    {
        builder.ToTable("authentication_sessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.SessionId).IsRequired();
        builder.HasIndex(session => session.SessionId).IsUnique().HasDatabaseName("ux_authentication_sessions_session_id");
        builder.Property(session => session.TokenHash).HasMaxLength(128).IsRequired();
        builder.HasIndex(session => session.TokenHash).HasDatabaseName("ix_authentication_sessions_token_hash");
        builder.Property(session => session.CreatedAt).IsRequired();
        builder.Property(session => session.ExpiresAt).IsRequired();
        builder.Property(session => session.LastUsedAt).IsRequired();
        builder.HasIndex(session => session.UserId).HasDatabaseName("ix_authentication_sessions_user_id");
        builder.HasIndex(session => session.OrganizationId).HasDatabaseName("ix_authentication_sessions_organization_id");
        builder.HasOne(session => session.User)
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(session => session.Organization)
            .WithMany()
            .HasForeignKey(session => session.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
