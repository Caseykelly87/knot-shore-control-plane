namespace KnotShoreControlPlane.Domain;

// Seasonality lever mirroring the simulation's seasonal profiles. Persisted as
// the simulation's slug strings (see ControlPlaneDbContext value conversion).
public enum SeasonalProfile
{
    SummerPeak,
    WinterPeak,
    HolidaySpike,
    Stable,
}
