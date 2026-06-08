using KnotShoreControlPlane.Domain;

namespace KnotShoreControlPlane.Features.Scenarios;

// Explicit input validation for scenario create/update. Minimal APIs on .NET 8 do
// not run data-annotation validation automatically, so the checks are explicit and
// shared by both write endpoints, returning per-field messages for ValidationProblem.
public static class ScenarioValidation
{
    public const int MaxNameLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxEconomicWindowRefLength = 200;
    public const int MaxStoreCount = 10_000;
    public const decimal MaxSigma = 5m;

    public static bool TryValidate(ScenarioRequest request, out Dictionary<string, string[]> errors)
    {
        var problems = new Dictionary<string, List<string>>();

        void Add(string field, string message)
        {
            if (!problems.TryGetValue(field, out var list))
            {
                list = new List<string>();
                problems[field] = list;
            }
            list.Add(message);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            Add("name", "Name is required.");
        }
        else if (request.Name.Length > MaxNameLength)
        {
            Add("name", $"Name must be {MaxNameLength} characters or fewer.");
        }

        if (request.Description is { Length: > MaxDescriptionLength })
        {
            Add("description", $"Description must be {MaxDescriptionLength} characters or fewer.");
        }

        if (request.EconomicWindowRef is { Length: > MaxEconomicWindowRefLength })
        {
            Add("economicWindowRef", $"EconomicWindowRef must be {MaxEconomicWindowRefLength} characters or fewer.");
        }

        if (request.StoreCount <= 0)
        {
            Add("storeCount", "StoreCount must be greater than zero.");
        }
        else if (request.StoreCount > MaxStoreCount)
        {
            Add("storeCount", $"StoreCount must be {MaxStoreCount} or fewer.");
        }

        if (request.EndDate < request.StartDate)
        {
            Add("endDate", "EndDate must be on or after StartDate.");
        }

        if (!Enum.IsDefined(request.SeasonalProfile))
        {
            Add("seasonalProfile", "SeasonalProfile is not a recognized value.");
        }

        if (request.YoyGrowthRate is < -1m or > 10m)
        {
            Add("yoyGrowthRate", "YoyGrowthRate must be between -1 and 10.");
        }

        if (request.AnomalyProbability is < 0m or > 1m)
        {
            Add("anomalyProbability", "AnomalyProbability must be between 0 and 1.");
        }

        ValidateNoise(request.Noise, Add);
        ValidateAnomalyWeights(request.AnomalyWeights, Add);

        errors = problems.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
        return errors.Count == 0;
    }

    private static void ValidateNoise(NoiseSettings noise, Action<string, string> add)
    {
        void Sigma(string field, decimal value)
        {
            if (value is < 0m || value > MaxSigma)
            {
                add(field, $"Noise sigma must be between 0 and {MaxSigma}.");
            }
        }

        Sigma("noise.salesSigma", noise.SalesSigma);
        Sigma("noise.laborSigma", noise.LaborSigma);
        Sigma("noise.ticketSigma", noise.TicketSigma);
        Sigma("noise.unitsSigma", noise.UnitsSigma);

        if (noise.ClipLower < 0m)
        {
            add("noise.clipLower", "ClipLower must be non-negative.");
        }

        if (noise.ClipUpper < noise.ClipLower)
        {
            add("noise.clipUpper", "ClipUpper must be on or above ClipLower.");
        }
    }

    private static void ValidateAnomalyWeights(AnomalyDistribution weights, Action<string, string> add)
    {
        void Weight(string field, decimal value)
        {
            if (value < 0m)
            {
                add(field, "Anomaly weight must be non-negative.");
            }
        }

        Weight("anomalyWeights.integrityBreachWeight", weights.IntegrityBreachWeight);
        Weight("anomalyWeights.missingDepartmentWeight", weights.MissingDepartmentWeight);
        Weight("anomalyWeights.marginOutlierWeight", weights.MarginOutlierWeight);
        Weight("anomalyWeights.duplicateRowWeight", weights.DuplicateRowWeight);
    }
}
