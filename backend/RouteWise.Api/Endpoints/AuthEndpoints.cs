using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Contracts.Auth;
using RouteWise.Api.Data;
using RouteWise.Api.Models;

namespace RouteWise.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/signup", SignupAsync).AllowAnonymous();

        app.MapPost("/api/auth/login", LoginAsync).AllowAnonymous();

        app.MapGet("/api/auth/me", GetCurrentUserAsync).RequireAuthorization();

        app.MapPost("/api/auth/logout", (Delegate)LogoutAsync).AllowAnonymous();
    }

    private static async Task<IResult> SignupAsync(HttpContext context,
        RouteWiseDbContext db, IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
            return Results.BadRequest(new { message = "A JSON request body is required." });

        SignupRequest? request;
        try
        {
            request = await context.Request.ReadFromJsonAsync<SignupRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { message = "Invalid JSON request body." });
        }

        var username = request?.Username?.Trim();
        var password = request?.Password;
        if (username is null || !Regex.IsMatch(username, @"\A[A-Za-z0-9_]{3,20}\z"))
            return Results.BadRequest(new
            {
                message = "Username must contain 3–20 ASCII letters, numbers, or underscores."
            });

        if (password is null || password.Length < 8)
            return Results.BadRequest(new { message = "Password must contain at least 8 characters." });

        var normalizedUsername = username.ToUpperInvariant();
        if (await db.Users.AnyAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken))
            return UsernameConflict();

        var user = new User { Username = username, NormalizedUsername = normalizedUsername };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException
            { SqliteExtendedErrorCode: 2067 })
        {
            // The unique index handles simultaneous signups that pass the earlier check.
            return UsernameConflict();
        }

        await SignInUserAsync(context, user);

        return Results.Json(new CurrentUserResponse(user.Id, user.Username),
            statusCode: StatusCodes.Status201Created);
    }

    private static async Task SignInUserAsync(HttpContext context, User user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.Username)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties { IsPersistent = false };
        var scheme = CookieAuthenticationDefaults.AuthenticationScheme;

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await context.SignInAsync(scheme, principal, properties);
            return;
        }

        // The existing cookie handler caches its ticket key even after SignOut.
        // Reusing it would give a copied old cookie access to the new account.
        await context.SignOutAsync(scheme);
        var incomingCookies = context.Request.Headers.Cookie;
        context.Request.Headers.Remove("Cookie");
        try
        {
            var handlers = new AuthenticationHandlerProvider(
                context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>());
            var handler = (IAuthenticationSignInHandler)(await handlers.GetHandlerAsync(context, scheme))!;
            await handler.SignInAsync(principal, properties);
        }
        finally
        {
            context.Request.Headers.Cookie = incomingCookies;
        }
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context,
        RouteWiseDbContext db,
        IPasswordHasher<User> passwordHasher,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
        {
            return Results.BadRequest(new
            {
                message = "A JSON request body is required."
            });
        }

        LoginRequest? request;

        try
        {
            request = await context.Request
                .ReadFromJsonAsync<LoginRequest>(cancellationToken);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new
            {
                message = "Invalid JSON request body."
            });
        }

        var username = request?.Username?.Trim();
        var password = request?.Password;

        if (string.IsNullOrEmpty(username) ||
            string.IsNullOrEmpty(password))
        {
            return InvalidLogin();
        }

        var normalizedUsername = username.ToUpperInvariant();

        var user = await db.Users.SingleOrDefaultAsync(
            user => user.NormalizedUsername == normalizedUsername,
            cancellationToken);

        if (user is null)
        {
            return InvalidLogin();
        }

        var verification = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            password);

        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidLogin();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            await db.SaveChangesAsync(cancellationToken);
        }

        await SignInUserAsync(context, user);

        return Results.Ok(
            new CurrentUserResponse(user.Id, user.Username));
    }

    private static IResult InvalidLogin() =>
        Results.Json(
            new { message = "Invalid username or password." },
            statusCode: StatusCodes.Status401Unauthorized);

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                user => user.Id == userId,
                cancellationToken);

        if (user is null)
        {
            await context.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return Results.Json(
                new { message = "Authentication required." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Ok(
            new CurrentUserResponse(user.Id, user.Username));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context)
    {
        await context.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return Results.NoContent();
    }

    private static IResult UsernameConflict() =>
        Results.Conflict(new { message = "Username is already taken." });
}
