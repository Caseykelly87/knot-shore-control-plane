using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace KnotShoreControlPlane.Tests;

public class ScenarioEndpointTests : IClassFixture<AuthTestFactory>
{
    private readonly AuthTestFactory _factory;

    public ScenarioEndpointTests(AuthTestFactory factory) => _factory = factory;

    // Business-correctness: creating a scenario persists it owned by the caller and
    // returns 201 with a generated GUID id.
    [Fact]
    public async Task Create_persists_the_scenario_owned_by_the_caller()
    {
        var client = await AuthFlow.AuthenticatedClientAsync(_factory, "create@scenarios.test");
        var callerId = await MeIdAsync(client);

        var response = await client.PostAsJsonAsync("/scenarios", ValidBody("Baseline"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(Guid.TryParse(doc.RootElement.GetProperty("id").GetString(), out var id));
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(callerId, doc.RootElement.GetProperty("ownerId").GetString());
    }

    // Business-correctness: a client cannot assign ownership; an ownerId in the body is
    // ignored and the created scenario belongs to the authenticated caller.
    [Fact]
    public async Task Create_ignores_owner_id_supplied_in_the_body()
    {
        var client = await AuthFlow.AuthenticatedClientAsync(_factory, "owner-spoof@scenarios.test");
        var callerId = await MeIdAsync(client);

        var body = new
        {
            ownerId = "someone-else",
            name = "Spoof attempt",
            storeCount = 8,
            startDate = "2024-01-01",
            endDate = "2025-12-31",
            seasonalProfile = 3,
            yoyGrowthRate = 0.025,
            noise = DefaultNoise(),
            anomalyProbability = 0.05,
            anomalyWeights = DefaultWeights(),
            seed = 42,
        };

        var response = await client.PostAsJsonAsync("/scenarios", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ownerId = doc.RootElement.GetProperty("ownerId").GetString();
        Assert.Equal(callerId, ownerId);
        Assert.NotEqual("someone-else", ownerId);
    }

    // Business-correctness: list returns only the caller's scenarios; a second user's
    // scenarios never appear in the first user's list.
    [Fact]
    public async Task List_returns_only_the_callers_scenarios()
    {
        var alice = await AuthFlow.AuthenticatedClientAsync(_factory, "alice@scenarios.test");
        var bob = await AuthFlow.AuthenticatedClientAsync(_factory, "bob@scenarios.test");

        var aliceId = await CreateAsync(alice, "Alice scenario");
        await CreateAsync(bob, "Bob scenario");

        var page = await GetPageAsync(alice);
        var ids = page.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToList();

        Assert.Contains(aliceId.ToString(), ids);
        Assert.Single(ids);
    }

    // Business-correctness: paging is bounded — the requested page is returned with the
    // owner-wide total, and a pageSize above the maximum is clamped server-side.
    [Fact]
    public async Task List_paginates_and_clamps_page_size()
    {
        var client = await AuthFlow.AuthenticatedClientAsync(_factory, "paging@scenarios.test");
        for (var i = 0; i < 3; i++)
        {
            await CreateAsync(client, $"Scenario {i}");
        }

        var firstPage = await GetPageAsync(client, page: 1, pageSize: 2);
        Assert.Equal(2, firstPage.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(3, firstPage.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, firstPage.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, firstPage.RootElement.GetProperty("totalPages").GetInt32());

        var clamped = await GetPageAsync(client, page: 1, pageSize: 1000);
        Assert.Equal(100, clamped.RootElement.GetProperty("pageSize").GetInt32());
    }

    // Business-correctness: get-one returns the caller's scenario, and returns 404 for a
    // scenario that is missing or owned by another user.
    [Fact]
    public async Task Get_one_is_owner_scoped()
    {
        var alice = await AuthFlow.AuthenticatedClientAsync(_factory, "get-alice@scenarios.test");
        var bob = await AuthFlow.AuthenticatedClientAsync(_factory, "get-bob@scenarios.test");
        var aliceScenario = await CreateAsync(alice, "Alice owns this");

        var owned = await alice.GetAsync($"/scenarios/{aliceScenario}");
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);

        var foreignAccess = await bob.GetAsync($"/scenarios/{aliceScenario}");
        Assert.Equal(HttpStatusCode.NotFound, foreignAccess.StatusCode);

        var missing = await alice.GetAsync($"/scenarios/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // Business-correctness: update persists changes for the owner, never alters ownership
    // or id, and returns 404 when the target belongs to another user.
    [Fact]
    public async Task Update_is_owner_scoped_and_preserves_owner_and_id()
    {
        var alice = await AuthFlow.AuthenticatedClientAsync(_factory, "update-alice@scenarios.test");
        var bob = await AuthFlow.AuthenticatedClientAsync(_factory, "update-bob@scenarios.test");
        var aliceId = await MeIdAsync(alice);
        var scenario = await CreateAsync(alice, "Original name");

        var update = ValidBody("Renamed");
        var owned = await alice.PutAsJsonAsync($"/scenarios/{scenario}", update);
        Assert.Equal(HttpStatusCode.OK, owned.StatusCode);
        using (var doc = JsonDocument.Parse(await owned.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Renamed", doc.RootElement.GetProperty("name").GetString());
            Assert.Equal(scenario.ToString(), doc.RootElement.GetProperty("id").GetString());
            Assert.Equal(aliceId, doc.RootElement.GetProperty("ownerId").GetString());
        }

        var foreignUpdate = await bob.PutAsJsonAsync($"/scenarios/{scenario}", ValidBody("Hijack"));
        Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);
    }

    // Business-correctness: delete removes the owner's scenario (204) and a delete against
    // another user's scenario returns 404 and leaves it intact.
    [Fact]
    public async Task Delete_is_owner_scoped()
    {
        var alice = await AuthFlow.AuthenticatedClientAsync(_factory, "delete-alice@scenarios.test");
        var bob = await AuthFlow.AuthenticatedClientAsync(_factory, "delete-bob@scenarios.test");
        var scenario = await CreateAsync(alice, "To delete");

        var foreignDelete = await bob.DeleteAsync($"/scenarios/{scenario}");
        Assert.Equal(HttpStatusCode.NotFound, foreignDelete.StatusCode);

        var ownedDelete = await alice.DeleteAsync($"/scenarios/{scenario}");
        Assert.Equal(HttpStatusCode.NoContent, ownedDelete.StatusCode);

        var afterwards = await alice.GetAsync($"/scenarios/{scenario}");
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    // Business-correctness: invalid input is rejected with 400 and not persisted.
    [Fact]
    public async Task Create_with_invalid_input_returns_400_and_does_not_persist()
    {
        var client = await AuthFlow.AuthenticatedClientAsync(_factory, "invalid@scenarios.test");

        var body = new
        {
            name = "Backwards window",
            storeCount = -5,
            startDate = "2025-12-31",
            endDate = "2024-01-01",
            seasonalProfile = 3,
            yoyGrowthRate = 0.025,
            noise = DefaultNoise(),
            anomalyProbability = 2.0,
            anomalyWeights = DefaultWeights(),
            seed = 42,
        };

        var response = await client.PostAsJsonAsync("/scenarios", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var page = await GetPageAsync(client);
        Assert.Equal(0, page.RootElement.GetProperty("totalCount").GetInt32());
    }

    // Business-correctness: every /scenarios endpoint requires authentication; an
    // unauthenticated request is rejected with 401.
    [Fact]
    public async Task Unauthenticated_requests_are_rejected()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/scenarios")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/scenarios", ValidBody("nope"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/scenarios/{Guid.NewGuid()}")).StatusCode);
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/scenarios", ValidBody(name));
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return Guid.Parse(doc.RootElement.GetProperty("id").GetString()!);
    }

    private static async Task<JsonDocument> GetPageAsync(HttpClient client, int? page = null, int? pageSize = null)
    {
        var query = new List<string>();
        if (page is not null) query.Add($"page={page}");
        if (pageSize is not null) query.Add($"pageSize={pageSize}");
        var url = "/scenarios" + (query.Count > 0 ? "?" + string.Join("&", query) : string.Empty);

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> MeIdAsync(HttpClient client)
    {
        var response = await client.GetAsync("/auth/me");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private static object ValidBody(string name) => new
    {
        name,
        description = "two years, eight stores",
        storeCount = 8,
        startDate = "2024-01-01",
        endDate = "2025-12-31",
        seasonalProfile = 3,
        yoyGrowthRate = 0.025,
        noise = DefaultNoise(),
        anomalyProbability = 0.05,
        anomalyWeights = DefaultWeights(),
        seed = 42,
        economicWindowRef = (string?)null,
    };

    private static object DefaultNoise() => new
    {
        salesSigma = 0.04,
        laborSigma = 0.02,
        ticketSigma = 0.06,
        unitsSigma = 0.05,
        clipLower = 0.88,
        clipUpper = 1.12,
    };

    private static object DefaultWeights() => new
    {
        integrityBreachWeight = 0.40,
        missingDepartmentWeight = 0.30,
        marginOutlierWeight = 0.20,
        duplicateRowWeight = 0.10,
    };
}
