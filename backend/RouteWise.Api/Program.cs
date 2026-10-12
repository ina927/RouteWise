using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using RouteWise.Api.Authentication;
using RouteWise.Api.Data;
using RouteWise.Api.Endpoints;
using RouteWise.Api.Models;
using RouteWise.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddScoped<JourneyService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<MemoryTicketStore>();
builder.Services.AddScoped<SessionCookieEvents>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RouteWise.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.EventsType = typeof(SessionCookieEvents);
    });
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<MemoryTicketStore>((options, store) => options.SessionStore = store);
builder.Services.AddAuthorization();

builder.Services.AddDbContext<RouteWiseDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("RouteWise")
        ?? "Data Source=routewise.db"));

var app = builder.Build();

app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { message = "An unexpected server error occurred." });
}));
app.UseMiddleware<TrustedOriginMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/api/health");
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapJourneyEndpoints();
app.MapUserEndpoints();
app.MapInvitationEndpoints();

app.Run();

public partial class Program { }
