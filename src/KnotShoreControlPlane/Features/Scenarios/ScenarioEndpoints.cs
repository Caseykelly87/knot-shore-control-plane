using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KnotShoreControlPlane.Data;
using KnotShoreControlPlane.Domain;
using Microsoft.EntityFrameworkCore;

namespace KnotShoreControlPlane.Features.Scenarios;

public static class ScenarioEndpoints
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapScenarioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/scenarios").RequireAuthorization();

        group.MapPost("", CreateAsync);
        group.MapGet("", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        ScenarioRequest request,
        ClaimsPrincipal principal,
        ControlPlaneDbContext db)
    {
        if (!ScenarioValidation.TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        var now = DateTime.UtcNow;
        var scenario = new Scenario
        {
            OwnerId = OwnerId(principal),
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(request, scenario);

        await db.Scenarios.AddAsync(scenario);
        await db.SaveChangesAsync();

        return Results.Created($"/scenarios/{scenario.Id}", ToResponse(scenario));
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal principal,
        ControlPlaneDbContext db,
        int? page,
        int? pageSize)
    {
        var currentPage = page is null or < 1 ? 1 : page.Value;
        var size = pageSize is null or < 1 ? DefaultPageSize : Math.Min(pageSize.Value, MaxPageSize);

        var owned = db.Scenarios.Where(s => s.OwnerId == OwnerId(principal));
        var total = await owned.CountAsync();
        var items = await owned
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip((currentPage - 1) * size)
            .Take(size)
            .ToListAsync();

        var totalPages = (int)Math.Ceiling(total / (double)size);
        var result = new PagedResult<ScenarioResponse>(
            items.Select(ToResponse).ToList(),
            currentPage,
            size,
            total,
            totalPages);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        ClaimsPrincipal principal,
        ControlPlaneDbContext db)
    {
        var scenario = await FindOwnedAsync(db, id, OwnerId(principal));
        return scenario is null ? Results.NotFound() : Results.Ok(ToResponse(scenario));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        ScenarioRequest request,
        ClaimsPrincipal principal,
        ControlPlaneDbContext db)
    {
        if (!ScenarioValidation.TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        var scenario = await FindOwnedAsync(db, id, OwnerId(principal));
        if (scenario is null)
        {
            return Results.NotFound();
        }

        Apply(request, scenario);
        scenario.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Results.Ok(ToResponse(scenario));
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ClaimsPrincipal principal,
        ControlPlaneDbContext db)
    {
        var scenario = await FindOwnedAsync(db, id, OwnerId(principal));
        if (scenario is null)
        {
            return Results.NotFound();
        }

        db.Scenarios.Remove(scenario);
        await db.SaveChangesAsync();

        return Results.NoContent();
    }

    // An id the caller does not own is indistinguishable from one that does not exist:
    // both resolve to null here and surface as 404, so ownership is never disclosed.
    private static Task<Scenario?> FindOwnedAsync(ControlPlaneDbContext db, Guid id, string ownerId) =>
        db.Scenarios.FirstOrDefaultAsync(s => s.Id == id && s.OwnerId == ownerId);

    private static string OwnerId(ClaimsPrincipal principal) =>
        principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated principal is missing the subject claim.");

    // Maps the client-supplied fields onto the entity, leaving ownership, id, and
    // CreatedAt untouched so they cannot be reassigned through a write.
    private static void Apply(ScenarioRequest request, Scenario scenario)
    {
        scenario.Name = request.Name;
        scenario.Description = request.Description;
        scenario.StoreCount = request.StoreCount;
        scenario.StartDate = request.StartDate;
        scenario.EndDate = request.EndDate;
        scenario.SeasonalProfile = request.SeasonalProfile;
        scenario.YoyGrowthRate = request.YoyGrowthRate;
        scenario.Noise = request.Noise;
        scenario.AnomalyProbability = request.AnomalyProbability;
        scenario.AnomalyWeights = request.AnomalyWeights;
        scenario.Seed = request.Seed;
        scenario.EconomicWindowRef = request.EconomicWindowRef;
    }

    private static ScenarioResponse ToResponse(Scenario s) => new(
        s.Id,
        s.OwnerId,
        s.Name,
        s.Description,
        s.CreatedAt,
        s.UpdatedAt,
        s.StoreCount,
        s.StartDate,
        s.EndDate,
        s.SeasonalProfile,
        s.YoyGrowthRate,
        s.Noise,
        s.AnomalyProbability,
        s.AnomalyWeights,
        s.Seed,
        s.EconomicWindowRef);
}
