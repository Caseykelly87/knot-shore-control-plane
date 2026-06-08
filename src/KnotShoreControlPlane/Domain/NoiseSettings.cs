namespace KnotShoreControlPlane.Domain;

// Realism-noise lever. Mirrors the simulation's NOISE_SIGMA_* family and the
// clip bounds applied to the N(1.0, sigma) multipliers. Mapped as an owned type
// (one column per field) by ControlPlaneDbContext. Defaults match the simulation.
public class NoiseSettings
{
    public decimal SalesSigma { get; set; } = 0.04m;
    public decimal LaborSigma { get; set; } = 0.02m;
    public decimal TicketSigma { get; set; } = 0.06m;
    public decimal UnitsSigma { get; set; } = 0.05m;
    public decimal ClipLower { get; set; } = 0.88m;
    public decimal ClipUpper { get; set; } = 1.12m;
}
