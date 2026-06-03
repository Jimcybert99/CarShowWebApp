using CarShowJudging.Core.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CarShowJudging.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleClass> VehicleClasses => Set<VehicleClass>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<SiteNote> SiteNotes => Set<SiteNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Vehicle>(e =>
        {
            e.HasOne(v => v.RegisteredBy)
                .WithMany()
                .HasForeignKey(v => v.RegisteredById)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(v => v.Owner)
                .WithMany()
                .HasForeignKey(v => v.OwnerId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            e.HasMany(v => v.Classes)
                .WithMany(c => c.Vehicles)
                .UsingEntity("VehicleVehicleClass");
        });

        builder.Entity<Score>(e =>
        {
            e.HasIndex(s => new { s.VehicleId, s.JudgeId }).IsUnique();

            e.HasOne(s => s.Vehicle)
                .WithMany(v => v.Scores)
                .HasForeignKey(s => s.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(s => s.Judge)
                .WithMany()
                .HasForeignKey(s => s.JudgeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.Ignore(s => s.Overall);
        });

        builder.Entity<SiteNote>(e =>
        {
            e.HasOne(n => n.Parent)
                .WithMany(n => n.Replies)
                .HasForeignKey(n => n.ParentNoteId)
                .OnDelete(DeleteBehavior.NoAction);

            e.HasOne(n => n.Vehicle)
                .WithMany()
                .HasForeignKey(n => n.VehicleId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);
        });
    }
}
