namespace KnotShoreControlPlane.Features.Auth;

// Bound from the "Jwt" configuration section. SigningKey is a secret supplied at
// runtime via environment (Jwt__SigningKey); appsettings holds a placeholder only.
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
}
