using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using KnotShoreControlPlane.Domain;
using Microsoft.AspNetCore.Identity;

namespace KnotShoreControlPlane.Features.Auth;

public record RegisterRequest(string Email, string Password, string? UserName);

public record LoginRequest(string Email, string Password);

public record TokenResponse(string AccessToken, DateTime ExpiresAtUtc, string TokenType = "Bearer");

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapGet("/me", Me).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<ApplicationUser> userManager)
    {
        var user = new ApplicationUser
        {
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? request.Email : request.UserName,
            Email = request.Email,
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
            return Results.ValidationProblem(errors);
        }

        return Results.Ok(new { user.Id, user.Email });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        // A single 401 for both an unknown user and a bad password so the response
        // never reveals which field was wrong.
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.Unauthorized();
        }

        var token = tokenService.CreateToken(user);
        return Results.Ok(new TokenResponse(token.Token, token.ExpiresAtUtc));
    }

    private static IResult Me(ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email);
        return Results.Ok(new { Id = id, Email = email });
    }
}
