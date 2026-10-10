using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Data;

namespace RouteWise.Api.Authentication;

public sealed class SessionCookieEvents(RouteWiseDbContext db) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id is null || !await db.Users.AnyAsync(u => u.Id == id, context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return context.Response.WriteAsJsonAsync(new { message = "Authentication required." });
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new { message = "Access denied." });
    }
}
