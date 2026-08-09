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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Vehicle>(e =>
        {
            e.HasIndex(v => v.EntryNumber).IsUnique();

            e.HasOne(v => v.RegisteredBy)
                .WithMany()
                .HasForeignKey(v => v.RegisteredById)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

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
                .OnDelete(DeleteBehavior.Cascade);

            e.Ignore(s => s.Overall);
        });
    }
}
