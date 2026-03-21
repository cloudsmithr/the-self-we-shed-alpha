using ApiServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ApiServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Player> Players => Set<Player>();
    public DbSet<GameWorld> GameWorlds => Set<GameWorld>();
    public DbSet<Fortress> Fortresses => Set<Fortress>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Player>(e =>
        {
            e.HasIndex(p => new { p.OAuthProvider, p.OAuthId }).IsUnique();
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
    }
}