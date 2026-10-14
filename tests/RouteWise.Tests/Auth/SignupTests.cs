using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RouteWise.Api.Data;
using RouteWise.Api.Models;

namespace RouteWise.Tests.Auth;

public sealed class SignupTests
{
    private SignupApplication app = null!;
    private HttpClient client = null!;

    [SetUp]
    public void SetUp()
    {
        app = new SignupApplication();
        client = app.CreateApiClient();
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Database.Migrate();
    }

    [TearDown]
    public void TearDown()
    {
        client.Dispose();
        app.Dispose();
    }

    [Test]
    public async Task Signup_SavesHashAndEstablishesSession()
    {
        const string password = " password with spaces ";
        var response = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "  River  ", password });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var cookie = response.Headers.GetValues("Set-Cookie").Single();
        using var scope = app.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.SingleAsync();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var session = await client.GetAsync("/_test/session");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "username" }));
            Assert.That(body.GetProperty("id").GetString(), Is.EqualTo(user.Id));
            Assert.That(body.GetProperty("username").GetString(), Is.EqualTo("River"));
            Assert.That(user.NormalizedUsername, Is.EqualTo("RIVER"));
            Assert.That(user.PasswordHash, Is.Not.EqualTo(password));
            Assert.That(hasher.VerifyHashedPassword(user, user.PasswordHash, password),
                Is.Not.EqualTo(PasswordVerificationResult.Failed));
            Assert.That(hasher.VerifyHashedPassword(user, user.PasswordHash, password.Trim()),
                Is.EqualTo(PasswordVerificationResult.Failed));
            Assert.That(cookie, Does.Contain("httponly").IgnoreCase);
            Assert.That(cookie, Does.Contain("samesite=strict").IgnoreCase);
            Assert.That(cookie, Does.Not.Contain("expires=").IgnoreCase);
            Assert.That(session.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
        Assert.That(await session.Content.ReadAsStringAsync(), Is.EqualTo(user.Id));

        // A separate client with the same cookie represents a new page/refresh.
        using var refreshed = app.CreateApiClient();
        refreshed.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        Assert.That((await refreshed.GetAsync("/_test/session")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase(null, "password1")]
    [TestCase("ab", "password1")]
    [TestCase("abcdefghijklmnopqrstu", "password1")]
    [TestCase("river-name", "password1")]
    [TestCase("rívér", "password1")]
    [TestCase("River", null)]
    [TestCase("River", "1234567")]
    public async Task InvalidInput_DoesNotCreateAnAccount(string? username, string? password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new { username, password });
        await AssertError(response, HttpStatusCode.BadRequest);
        Assert.That(response.Headers.Contains("Set-Cookie"), Is.False);
        using var scope = app.Services.CreateScope();
        Assert.That(await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.CountAsync(), Is.Zero);
    }

    [TestCase("abc")]
    [TestCase("abcdefghijklmnopqrst")]
    public async Task UsernameBoundaries_AndEightCharacterPassword_AreAccepted(string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new { username, password = "12345678" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task DuplicateUsername_IsCaseInsensitive()
    {
        await client.PostAsJsonAsync("/api/auth/signup", new { username = "River", password = "password1" });
        var duplicate = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = " river ", password = "different-password" });
        await AssertError(duplicate, HttpStatusCode.Conflict);
        Assert.That(duplicate.Headers.Contains("Set-Cookie"), Is.False);
        using var scope = app.Services.CreateScope();
        Assert.That(await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task SimultaneousSignups_ForSameUsername_ReturnOneConflict()
    {
        // Both requests must pass their availability lookup before either inserts.
        var barrier = new SignupBarrier();
        using var racingApp = app.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<RouteWiseDbContext>(options => options.AddInterceptors(barrier))));
        using var first = racingApp.CreateClient();
        using var second = racingApp.CreateClient();
        first.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        second.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/auth/signup", new { username = "Racer", password = "password1" }),
            second.PostAsJsonAsync("/api/auth/signup", new { username = "racer", password = "password2" }));
        Assert.That(responses.Select(r => r.StatusCode),
            Is.EquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
        await AssertError(responses.Single(r => r.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict);
    }

    [TestCase("{")]
    [TestCase("")]
    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("{\"username\":42,\"password\":\"password1\"}")]
    public async Task InvalidJson_ReturnsContractError(string json)
    {
        var response = await client.PostAsync("/api/auth/signup", new StringContent(json, Encoding.UTF8, "application/json"));
        await AssertError(response, HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task FormSubmission_IsRejected()
    {
        var response = await client.PostAsync("/api/auth/signup",
            new FormUrlEncodedContent(new Dictionary<string, string>
            { ["username"] = "River", ["password"] = "password1" }));
        await AssertError(response, HttpStatusCode.BadRequest);
    }

    [TestCase(null)]
    [TestCase("null")]
    [TestCase("https://untrusted.example")]
    [TestCase("http://localhost:5173.attacker.example")]
    public async Task UntrustedOrMissingOrigin_IsRejected(string? origin)
    {
        client.DefaultRequestHeaders.Remove("Origin");
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);
        var response = await client.PostAsJsonAsync("/api/auth/signup", new { username = "River", password = "password1" });
        await AssertError(response, HttpStatusCode.Forbidden);
        using var scope = app.Services.CreateScope();
        Assert.That(await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.CountAsync(), Is.Zero);
    }

    [Test]
    public async Task DeletedAccount_CannotUseExistingSession()
    {
        await client.PostAsJsonAsync("/api/auth/signup", new { username = "River", password = "password1" });
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.ExecuteDeleteAsync();
        await AssertError(await client.GetAsync("/_test/session"), HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Signout_InvalidatesServerTicketEvenIfCookieIsReplayed()
    {
        var signup = await client.PostAsJsonAsync("/api/auth/signup", new { username = "River", password = "password1" });
        var cookie = signup.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        await client.PostAsync("/_test/signout", null);
        using var replay = app.CreateApiClient();
        replay.DefaultRequestHeaders.Add("Cookie", cookie);
        await AssertError(await replay.GetAsync("/_test/session"), HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task ApiAuthenticationFailures_DoNotRedirect()
    {
        var unauthenticated = await client.GetAsync("/_test/session");
        await AssertError(unauthenticated, HttpStatusCode.Unauthorized);
        Assert.That(unauthenticated.Headers.Location, Is.Null);
        var forbidden = await client.GetAsync("/_test/forbidden");
        await AssertError(forbidden, HttpStatusCode.Forbidden);
        Assert.That(forbidden.Headers.Location, Is.Null);
    }

    [Test]
    public async Task ProductionCookie_RequiresHttps()
    {
        using var production = new SignupApplication("Production");
        using var browser = production.CreateApiClient();
        using (var scope = production.Services.CreateScope())
            scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Database.Migrate();
        var response = await browser.PostAsJsonAsync("/api/auth/signup", new { username = "River", password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.GetValues("Set-Cookie").Single(), Does.Contain("secure").IgnoreCase);
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.That(response.StatusCode, Is.EqualTo(status));
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "message" }));
        Assert.That(error.GetProperty("message").GetString(), Is.Not.Empty);
    }

    private sealed class SignupBarrier : SaveChangesInterceptor
    {
        private int arrivals;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrivals) == 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
}

internal sealed class SignupApplication(string environment = "Development") : WebApplicationFactory<Program>
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"routewise-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:RouteWise"] = $"Data Source={databasePath};Pooling=False",
                ["Authentication:AllowedOrigins:0"] = "http://localhost:5173"
            }));
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, SessionProbe>());
    }

    public HttpClient CreateApiClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            File.Delete(databasePath);
            File.Delete(databasePath + "-wal");
            File.Delete(databasePath + "-shm");
        }
    }

    // Test-only probes verify real cookie authentication without adding /auth/me
    // or logout to the production API before their implementation step.
    private sealed class SessionProbe : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/_test/session")
                {
                    var session = await context.AuthenticateAsync();
                    if (!session.Succeeded) await context.ChallengeAsync();
                    else await context.Response.WriteAsync(session.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
                }
                else if (context.Request.Path == "/_test/signout")
                {
                    await context.SignOutAsync();
                    context.Response.StatusCode = 204;
                }
                else if (context.Request.Path == "/_test/forbidden") await context.ForbidAsync();
                else await nextMiddleware();
            });
            next(app);
        };
    }
}
