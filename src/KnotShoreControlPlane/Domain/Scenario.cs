namespace KnotShoreControlPlane.Domain;

// A scenario definition: the metadata describing one generation run. Holds
// definition levers only — never generated sales rows, which are data-plane
// volume produced elsewhere. Defaults mirror the simulation's pinned values.
public class Scenario
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Owning Identity user (ApplicationUser.Id).
    public string OwnerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Scope of the generation.
    public int StoreCount { get; set; } = 8;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    // Generation levers mirroring the simulation.
    public SeasonalProfile SeasonalProfile { get; set; } = SeasonalProfile.Stable;
    public decimal YoyGrowthRate { get; set; } = 0.025m;
    public NoiseSettings Noise { get; set; } = new();
    public decimal AnomalyProbability { get; set; } = 0.05m;
    public AnomalyDistribution AnomalyWeights { get; set; } = new();

    // Determinism: same seed plus same levers reproduces a generation.
    public long Seed { get; set; } = 42;

    // Forward hook for replaying against real economic history; unused until the
    // economic-data plane exists.
    public string? EconomicWindowRef { get; set; }
}
