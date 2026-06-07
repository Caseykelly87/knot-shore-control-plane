namespace KnotShoreControlPlane.Domain;

// Anomaly-type weighting lever. Mirrors the simulation's fixed set of anomaly
// injection weights; the four weights are expected to sum to 1.0. Mapped as an
// owned type (one column per weight) by ControlPlaneDbContext. Defaults match
// the simulation.
public class AnomalyDistribution
{
    public decimal IntegrityBreachWeight { get; set; } = 0.40m;
    public decimal MissingDepartmentWeight { get; set; } = 0.30m;
    public decimal MarginOutlierWeight { get; set; } = 0.20m;
    public decimal DuplicateRowWeight { get; set; } = 0.10m;
}
