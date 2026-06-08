using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using KnotShoreControlPlane.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace KnotShoreControlPlane.Tests;

public class AuthEndpointTests : IClassFixture<AuthTestFactory>
{
    private const string ValidPassword = "Test_Password_123!";

    private readonly AuthTestFactory _factory;

    public AuthEndpointTests(AuthTestFactory factory) => _factory = factory;

    // Business-correctness: registering with a valid email/password succeeds and the
    // user is persisted, retrievable afterward by email.
    [Fact]
    public async Task Register_succeeds_and_persists_the_user()
    {
        var client = _factory.CreateClient();
        const string email = "register@example.test";

        var response = await client.PostAsJsonAsync("/auth/register", new { email, password = ValidPassword });

        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var stored = await userManager.FindByEmailAsync(email);
        Assert.NotNull(stored);
    }

    // Business-correctness: a successful login returns a well-formed JWT whose subject
    // and email claims identify the authenticated user.
    [Fact]
    public async Task Login_with_correct_credentials_returns_a_wellformed_jwt()
    {
        var client = _factory.CreateClient();
        const string email = "login-ok@example.test";
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password = ValidPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await ReadAccessTokenAsync(response);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expectedId = await GetUserIdAsync(email);
        Assert.Equal(expectedId, jwt.Subject);
        Assert.Equal(email, jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Email).Value);
    }

    // Business-correctness: a wrong password is rejected with 401, returns no token, and
    // does not reveal which field was wrong (same result an unknown user would get).
    [Fact]
    public async Task Login_with_wrong_password_is_unauthorized_and_returns_no_token()
    {
        var client = _factory.CreateClient();
        const string email = "login-bad@example.test";
        await RegisterAsync(client, email);

        var response = await client.PostAsJsonAsync("/auth/login", new { email, password = "Wrong_Password_999!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    // Business-correctness: a valid token grants access to the protected endpoint, which
    // returns the caller's own identity read from the token claims.
    [Fact]
    public async Task Protected_endpoint_returns_the_caller_identity_with_a_valid_token()
    {
        var client = _factory.CreateClient();
        const string email = "me@example.test";
        await RegisterAsync(client, email);
        var token = await LoginAsync(client, email);

        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(await GetUserIdAsync(email), doc.RootElement.GetProperty("id").GetString());
        Assert.Equal(email, doc.RootElement.GetProperty("email").GetString());
    }

    // Business-correctness: the protected endpoint denies access (401) when no token or a
    // malformed token is presented.
    [Theory]
    [InlineData(null)]
    [InlineData("this.is.not.a.jwt")]
    public async Task Protected_endpoint_is_unauthorized_without_a_valid_token(string? bearer)
    {
        var client = _factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Business-correctness: a token signed with the expected key but past its expiry is
    // rejected (401) because lifetime validation is enforced.
    [Fact]
    public async Task Protected_endpoint_is_unauthorized_with_an_expired_token()
    {
        var client = _factory.CreateClient();
        var expired = CreateExpiredToken("expired@example.test");

        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expired);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/auth/register", new { email, password = ValidPassword });
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new { email, password = ValidPassword });
        response.EnsureSuccessStatusCode();
        return await ReadAccessTokenAsync(response);
    }

    private static async Task<string> ReadAccessTokenAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("accessToken").GetString()!;
    }

    private async Task<string?> GetUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return user!.Id;
    }

    private static string CreateExpiredToken(string subject)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthTestFactory.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var issuedAt = DateTime.UtcNow.AddHours(-2);
        var token = new JwtSecurityToken(
            issuer: AuthTestFactory.Issuer,
            audience: AuthTestFactory.Audience,
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) },
            notBefore: issuedAt,
            expires: issuedAt.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
