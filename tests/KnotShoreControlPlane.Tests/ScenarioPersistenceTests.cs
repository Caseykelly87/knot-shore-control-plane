using KnotShoreControlPlane.Data;
using KnotShoreControlPlane.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KnotShoreControlPlane.Tests;

public class ScenarioPersistenceTests
{
    private static (ControlPlaneDbContext Context, SqliteConnection Connection) NewContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ControlPlaneDbContext(options);
        context.Database.EnsureCreated();
        return (context, connection);
    }

    // Business-correctness: a Scenario definition persists and reads back with every
    // lever intact, including the owned-type noise and anomaly-weight mappings and
    // the seasonal-profile slug conversion.
    [Fact]
    public async Task Scenario_round_trips_through_the_database()
    {
        var (context, connection) = NewContext();
        try
        {
            var owner = new ApplicationUser { UserName = "owner@example.test", Email = "owner@example.test" };
            context.Users.Add(owner);
            await context.SaveChangesAsync();

            var scenario = new Scenario
            {
                OwnerId = owner.Id,
                Name = "Holiday-spike baseline",
                Description = "ten stores over two years",
                CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                StoreCount = 10,
                StartDate = new DateOnly(2024, 1, 1),
                EndDate = new DateOnly(2025, 12, 31),
                SeasonalProfile = SeasonalProfile.HolidaySpike,
                YoyGrowthRate = 0.031m,
                Noise = new NoiseSettings
                {
                    SalesSigma = 0.05m,
                    LaborSigma = 0.03m,
                    TicketSigma = 0.07m,
                    UnitsSigma = 0.06m,
                    ClipLower = 0.85m,
                    ClipUpper = 1.15m,
                },
                AnomalyProbability = 0.07m,
                AnomalyWeights = new AnomalyDistribution
                {
                    IntegrityBreachWeight = 0.35m,
                    MissingDepartmentWeight = 0.25m,
                    MarginOutlierWeight = 0.25m,
                    DuplicateRowWeight = 0.15m,
                },
                Seed = 12345,
                EconomicWindowRef = "recession-2008",
            };
            context.Scenarios.Add(scenario);
            await context.SaveChangesAsync();
            var id = scenario.Id;

            var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseSqlite(connection)
                .Options;
            using var verify = new ControlPlaneDbContext(options);
            var loaded = await verify.Scenarios.SingleAsync(s => s.Id == id);

            Assert.Equal(owner.Id, loaded.OwnerId);
            Assert.Equal("Holiday-spike baseline", loaded.Name);
            Assert.Equal("ten stores over two years", loaded.Description);
            Assert.Equal(10, loaded.StoreCount);
            Assert.Equal(new DateOnly(2024, 1, 1), loaded.StartDate);
            Assert.Equal(new DateOnly(2025, 12, 31), loaded.EndDate);
            Assert.Equal(SeasonalProfile.HolidaySpike, loaded.SeasonalProfile);
            Assert.Equal(0.031m, loaded.YoyGrowthRate);
            Assert.Equal(0.05m, loaded.Noise.SalesSigma);
            Assert.Equal(0.03m, loaded.Noise.LaborSigma);
            Assert.Equal(0.07m, loaded.Noise.TicketSigma);
            Assert.Equal(0.06m, loaded.Noise.UnitsSigma);
            Assert.Equal(0.85m, loaded.Noise.ClipLower);
            Assert.Equal(1.15m, loaded.Noise.ClipUpper);
            Assert.Equal(0.07m, loaded.AnomalyProbability);
            Assert.Equal(0.35m, loaded.AnomalyWeights.IntegrityBreachWeight);
            Assert.Equal(0.25m, loaded.AnomalyWeights.MissingDepartmentWeight);
            Assert.Equal(0.25m, loaded.AnomalyWeights.MarginOutlierWeight);
            Assert.Equal(0.15m, loaded.AnomalyWeights.DuplicateRowWeight);
            Assert.Equal(12345, loaded.Seed);
            Assert.Equal("recession-2008", loaded.EconomicWindowRef);
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }

    // Structural: the Scenario maps to a GUID primary key and declares the indexes
    // (OwnerId, CreatedAt, Name) that later query paths depend on.
    [Fact]
    public void Scenario_model_has_guid_key_and_declared_indexes()
    {
        var (context, connection) = NewContext();
        try
        {
            var entity = context.Model.FindEntityType(typeof(Scenario))!;

            var keyProperty = Assert.Single(entity.FindPrimaryKey()!.Properties);
            Assert.Equal(nameof(Scenario.Id), keyProperty.Name);
            Assert.Equal(typeof(Guid), keyProperty.ClrType);

            var indexedProperties = entity.GetIndexes()
                .Select(index => Assert.Single(index.Properties).Name)
                .ToHashSet();
            Assert.Contains(nameof(Scenario.OwnerId), indexedProperties);
            Assert.Contains(nameof(Scenario.CreatedAt), indexedProperties);
            Assert.Contains(nameof(Scenario.Name), indexedProperties);
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }
}
