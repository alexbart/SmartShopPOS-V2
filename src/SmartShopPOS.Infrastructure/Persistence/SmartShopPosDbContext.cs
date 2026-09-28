using Microsoft.EntityFrameworkCore;
using SmartShopPOS.Domain.Identity;

namespace SmartShopPOS.Infrastructure.Persistence;

public class SmartShopPosDbContext : DbContext
{
    public SmartShopPosDbContext(DbContextOptions<SmartShopPosDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartShopPosDbContext).Assembly);
    }
}
