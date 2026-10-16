using Microsoft.EntityFrameworkCore;

using CartridgeApp.Models;

namespace CartridgeApp.Data;

/// <summary>
/// Entity Framework Core context for the Cartridge application database.
/// </summary>
public class CartridgeDbContext : DbContext
{
    public CartridgeDbContext(DbContextOptions<CartridgeDbContext> options)
        : base(options)
    {
    }

    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(game => game.Id);
            entity.Property(game => game.Id).ValueGeneratedOnAdd();
            entity.Property(game => game.Title).IsRequired().HasMaxLength(200);
            entity.Property(game => game.Developer).IsRequired().HasMaxLength(200);
            entity.Property(game => game.Platform).IsRequired().HasMaxLength(100);
            entity.Property(game => game.Status).HasConversion<string>().IsRequired();
            entity.Property(game => game.Notes).HasMaxLength(4000);
            entity.HasIndex(game => game.Status);
            entity.HasIndex(game => game.Platform);
        });

        base.OnModelCreating(modelBuilder);
    }
}
