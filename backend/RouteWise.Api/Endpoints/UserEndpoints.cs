using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Contracts.Users;
using RouteWise.Api.Data;

namespace RouteWise.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/api/users", SearchUsersAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> SearchUsersAsync(
        string? query,
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var term = query?.Trim().ToUpperInvariant();

        // An empty search returns no suggestions.
        if (string.IsNullOrEmpty(term))
            return Results.Ok(Array.Empty<UserSummaryResponse>());

        var users = await db.Users
            .AsNoTracking()
            .Where(user =>
                user.Id != userId &&
                user.NormalizedUsername.Contains(term))
            .OrderBy(user => user.NormalizedUsername)
            .Select(user => new UserSummaryResponse(
                user.Id,
                user.Username))
            .ToListAsync(cancellationToken);

        return Results.Ok(users);
    }
}