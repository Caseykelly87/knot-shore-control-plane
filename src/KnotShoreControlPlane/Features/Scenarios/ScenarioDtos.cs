using KnotShoreControlPlane.Domain;

namespace KnotShoreControlPlane.Features.Scenarios;

// Client-supplied scenario definition. Ownership, id, and timestamps are never
// accepted from the client — they are set by the service from the token and the
// clock — so they are absent here.
public class ScenarioRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int StoreCount { get; set; } = 8;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public SeasonalProfile SeasonalProfile { get; set; } = SeasonalProfile.Stable;
    public decimal YoyGrowthRate { get; set; } = 0.025m;
    public NoiseSettings Noise { get; set; } = new();
    public decimal AnomalyProbability { get; set; } = 0.05m;
    public AnomalyDistribution AnomalyWeights { get; set; } = new();

    public long Seed { get; set; } = 42;
    public string? EconomicWindowRef { get; set; }
}

public record ScenarioResponse(
    Guid Id,
    string OwnerId,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int StoreCount,
    DateOnly StartDate,
    DateOnly EndDate,
    SeasonalProfile SeasonalProfile,
    decimal YoyGrowthRate,
    NoiseSettings Noise,
    decimal AnomalyProbability,
    AnomalyDistribution AnomalyWeights,
    long Seed,
    string? EconomicWindowRef);

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
