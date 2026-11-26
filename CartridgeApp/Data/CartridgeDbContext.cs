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
    public DbSet<PlayLog> PlayLogs => Set<PlayLog>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

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
            entity.Property(game => game.CoverUrl).HasMaxLength(1000);
            entity.HasIndex(game => game.Status);
            entity.HasIndex(game => game.Platform);

            entity.HasMany(game => game.PlayLogs)
                .WithOne(log => log.Game)
                .HasForeignKey(log => log.GameId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayLog>(entity =>
        {
            entity.HasKey(log => log.Id);
            entity.Property(log => log.Id).ValueGeneratedOnAdd();
            // Stored without time zone: the repository always writes Unspecified-kind values,
            // which avoids the Npgsql "Kind=Local" exception that timestamptz would raise.
            entity.Property(log => log.LoggedDate).HasColumnType("timestamp without time zone");
            entity.Property(log => log.StatusAfterSession).HasConversion<string>().IsRequired();
            entity.Property(log => log.ReviewText).HasMaxLength(4000);
            entity.HasIndex(log => log.GameId);
            entity.HasIndex(log => log.LoggedDate);
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(profile => profile.Id);
            entity.Property(profile => profile.Id).ValueGeneratedOnAdd();
            entity.Property(profile => profile.DisplayName).IsRequired().HasMaxLength(100);
            entity.Property(profile => profile.Handle).IsRequired().HasMaxLength(60);
            entity.Property(profile => profile.Bio).IsRequired().HasMaxLength(1000);
            entity.Property(profile => profile.AvatarUrl).HasMaxLength(1000);
            entity.Property(profile => profile.FavoritePlatform).IsRequired().HasMaxLength(100);
            entity.Property(profile => profile.ShowcaseGameIdsCsv).IsRequired().HasMaxLength(200);
        });

        base.OnModelCreating(modelBuilder);
    }
}