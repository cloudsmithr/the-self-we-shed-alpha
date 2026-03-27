using ApiServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<GameWorld> GameWorlds => Set<GameWorld>();
    public DbSet<Fortress> Fortresses => Set<Fortress>();
    public DbSet<BuildingType> BuildingTypes => Set<BuildingType>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<PlayerContribution> PlayerContributions => Set<PlayerContribution>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>(e =>
        {
            e.HasIndex(p => new { p.OAuthProvider, p.OAuthId })
                .IsUnique();
            e.HasIndex(p => p.DisplayName)
                .IsUnique();
            e.Property(p => p.DisplayName)
                .HasMaxLength(64);
            e.Property(p => p.OAuthProvider)
                .HasMaxLength(32);
            e.Property(p => p.OAuthId)
                .HasMaxLength(256);
        });

        modelBuilder.Entity<GameWorld>(e =>
        {
            e.Property(x => x.Status)
                .HasConversion<string>();

            e.Property(x => x.Config)
                .HasColumnType("jsonb");
        });

        modelBuilder.Entity<Fortress>(e =>
        {
            e.Property(x => x.Owner)
                .HasConversion<string>();

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            e.HasOne(x => x.GameWorld)
                .WithMany(g => g.Fortresses)
                .HasForeignKey(x => x.GameWorldId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BuildingType>(e =>
        {
            e.HasOne(x => x.GameWorld)
                .WithMany(g => g.BuildingTypes)
                .HasForeignKey(x => x.GameWorldId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Building>(e =>
        {
            e.HasOne(x => x.Fortress)
                .WithMany()
                .HasForeignKey(x => x.FortressId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.BuildingType)
                .WithMany(bt => bt.Buildings)
                .HasForeignKey(x => x.BuildingTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.BuiltBy)
                .WithMany()
                .HasForeignKey(x => x.BuiltById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlayerContribution>(e =>
        {
            e.Property(x => x.ActionType)
                .HasConversion<string>();

            e.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.GameWorld)
                .WithMany()
                .HasForeignKey(x => x.GameWorldId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.TargetFortress)
                .WithMany()
                .HasForeignKey(x => x.TargetFortressId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.BuildingType)
                .WithMany()
                .HasForeignKey(x => x.BuildingTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(rt => rt.Id);

            e.HasIndex(rt => rt.TokenHash).IsUnique();
            e.HasIndex(rt => rt.PlayerId);
            e.HasIndex(rt => rt.FamilyId);

            e.HasOne(rt => rt.Player)
                .WithMany()
                .HasForeignKey(rt => rt.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
