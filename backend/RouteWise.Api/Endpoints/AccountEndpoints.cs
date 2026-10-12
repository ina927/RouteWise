using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Data;

namespace RouteWise.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapDelete("/api/account", DeleteAccountAsync).RequireAuthorization();
    }

    private static async Task<IResult> DeleteAccountAsync(HttpContext context,
        RouteWiseDbContext db, CancellationToken cancellationToken)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Results.Json(new { message = "Authentication required." },
                statusCode: StatusCodes.Status401Unauthorized);

        // SQLite applies the configured cascades atomically with this deletion.
        await db.Users.Where(user => user.Id == userId).ExecuteDeleteAsync(cancellationToken);

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }
}
