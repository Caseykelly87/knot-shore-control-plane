using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KnotShoreControlPlane.Tests;

// Test helper: registers and logs in a user against the hosted app and returns an
// HttpClient carrying that user's bearer token, so ownership-scoped endpoints can be
// exercised as a specific caller.
internal static class AuthFlow
{
    public const string Password = "Test_Password_123!";

    public static async Task<HttpClient> AuthenticatedClientAsync(
        WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        register.EnsureSuccessStatusCode();

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
