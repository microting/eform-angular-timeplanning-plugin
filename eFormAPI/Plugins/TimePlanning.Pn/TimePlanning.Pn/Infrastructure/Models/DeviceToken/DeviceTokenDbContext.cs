namespace TimePlanning.Pn.Infrastructure.Models.DeviceToken;

using Microsoft.EntityFrameworkCore;

public class DeviceTokenDbContext : DbContext
{
    public DeviceTokenDbContext(DbContextOptions<DeviceTokenDbContext> options)
        : base(options)
    {
    }

    public DbSet<DeviceToken> DeviceTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DeviceToken>(entity =>
        {
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.SdkSiteId);
        });
    }
}
