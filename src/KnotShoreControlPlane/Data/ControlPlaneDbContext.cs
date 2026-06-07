using KnotShoreControlPlane.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace KnotShoreControlPlane.Data;

public class ControlPlaneDbContext : IdentityDbContext<ApplicationUser>
{
    public ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options)
        : base(options)
    {
    }

    public DbSet<Scenario> Scenarios => Set<Scenario>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Scenario>(scenario =>
        {
            scenario.HasKey(s => s.Id);
            scenario.Property(s => s.Id).ValueGeneratedNever();

            scenario.Property(s => s.OwnerId).IsRequired();
            scenario.Property(s => s.Name).IsRequired().HasMaxLength(200);
            scenario.Property(s => s.Description).HasMaxLength(2000);
            scenario.Property(s => s.EconomicWindowRef).HasMaxLength(200);

            scenario.Property(s => s.YoyGrowthRate).HasPrecision(9, 6);
            scenario.Property(s => s.AnomalyProbability).HasPrecision(9, 6);

            scenario.Property(s => s.SeasonalProfile)
                .HasConversion(
                    v => v == SeasonalProfile.SummerPeak ? "summer-peak"
                        : v == SeasonalProfile.WinterPeak ? "winter-peak"
                        : v == SeasonalProfile.HolidaySpike ? "holiday-spike"
                        : "stable",
                    s => s == "summer-peak" ? SeasonalProfile.SummerPeak
                        : s == "winter-peak" ? SeasonalProfile.WinterPeak
                        : s == "holiday-spike" ? SeasonalProfile.HolidaySpike
                        : SeasonalProfile.Stable)
                .HasMaxLength(50);

            scenario.OwnsOne(s => s.Noise, noise =>
            {
                noise.Property(n => n.SalesSigma).HasPrecision(9, 6);
                noise.Property(n => n.LaborSigma).HasPrecision(9, 6);
                noise.Property(n => n.TicketSigma).HasPrecision(9, 6);
                noise.Property(n => n.UnitsSigma).HasPrecision(9, 6);
                noise.Property(n => n.ClipLower).HasPrecision(9, 6);
                noise.Property(n => n.ClipUpper).HasPrecision(9, 6);
            });

            scenario.OwnsOne(s => s.AnomalyWeights, weights =>
            {
                weights.Property(w => w.IntegrityBreachWeight).HasPrecision(9, 6);
                weights.Property(w => w.MissingDepartmentWeight).HasPrecision(9, 6);
                weights.Property(w => w.MarginOutlierWeight).HasPrecision(9, 6);
                weights.Property(w => w.DuplicateRowWeight).HasPrecision(9, 6);
            });

            scenario.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(s => s.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            scenario.HasIndex(s => s.OwnerId);
            scenario.HasIndex(s => s.CreatedAt);
            scenario.HasIndex(s => s.Name);
        });
    }
}
