using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using RouteWise.Api.Data;
using RouteWise.Api.Models;

namespace RouteWise.Tests.Auth;

public sealed class AuthenticationTests
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
    public async Task SignupLogoutLoginAndRefresh_ReturnTheSamePublicIdentity()
    {
        var signup = await Signup();
        var created = await signup.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetString();
        await AssertIdentity(await client.GetAsync("/api/auth/me"), id, "River");
        Assert.That((await client.PostAsync("/api/auth/logout", null)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        await AssertError(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);

        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = " river ", password = "password1" });
        await AssertIdentity(login, id, "River");
        var me = await client.GetAsync("/api/auth/me");
        await AssertIdentity(me, id, "River");
        Assert.That(me.Headers.CacheControl?.NoStore, Is.True);

        using var refreshed = app.CreateApiClient();
        refreshed.DefaultRequestHeaders.Add("Cookie", SessionCookie(login));
        await AssertIdentity(await refreshed.GetAsync("/api/auth/me"), id, "River");
    }

    [TestCase("River", "wrong-password")]
    [TestCase("Unknown", "password1")]
    [TestCase("River", "Password1")]
    [TestCase("River", " password1 ")]
    [TestCase(null, "password1")]
    [TestCase("River", null)]
    [TestCase("", "password1")]
    [TestCase("River", "")]
    public async Task InvalidCredentials_ReturnGeneric401AndNoSession(string? username, string? password)
    {
        await Signup();
        await client.PostAsync("/api/auth/logout", null);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        await AssertError(response, HttpStatusCode.Unauthorized, "Invalid username or password.");
        Assert.That(response.Headers.Contains("Set-Cookie"), Is.False);
        await AssertError(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task PasswordWhitespace_IsPreservedAtLogin()
    {
        const string password = " password1 ";
        await client.PostAsJsonAsync("/api/auth/signup", new { username = "River", password });
        await client.PostAsync("/api/auth/logout", null);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = "River", password });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [TestCase("{")]
    [TestCase("")]
    [TestCase("[]")]
    [TestCase("{\"username\":42}")]
    public async Task MalformedLogin_Returns400(string json)
    {
        await AssertError(await client.PostAsync("/api/auth/login", new StringContent(json, Encoding.UTF8, "application/json")),
            HttpStatusCode.BadRequest);
    }

    [TestCase("null")]
    [TestCase("{}")]
    public async Task MissingCredentials_Return401(string json)
    {
        await AssertError(await client.PostAsync("/api/auth/login", new StringContent(json, Encoding.UTF8, "application/json")),
            HttpStatusCode.Unauthorized, "Invalid username or password.");
    }

    [Test]
    public async Task LoginRequiresJson()
    {
        await AssertError(await client.PostAsync("/api/auth/login", new StringContent("username=River")), HttpStatusCode.BadRequest);
    }

    [TestCase("/api/auth/login", null)]
    [TestCase("/api/auth/login", "https://untrusted.example")]
    [TestCase("/api/auth/logout", null)]
    [TestCase("/api/auth/logout", "https://untrusted.example")]
    public async Task UntrustedOrigin_IsRejectedWithoutChangingSession(string path, string? origin)
    {
        await Signup();
        client.DefaultRequestHeaders.Remove("Origin");
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);
        await AssertError(await client.PostAsJsonAsync(path, new { username = "River", password = "password1" }),
            HttpStatusCode.Forbidden);
        Assert.That((await client.GetAsync("/api/auth/me")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Logout_RemovesServerSessionAndIsRepeatable()
    {
        var signup = await Signup();
        var oldCookie = SessionCookie(signup);
        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.That(logout.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await logout.Content.ReadAsStringAsync(), Is.Empty);
        await AssertError(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
        using var replay = app.CreateApiClient();
        replay.DefaultRequestHeaders.Add("Cookie", oldCookie);
        await AssertError(await replay.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
        Assert.That((await client.PostAsync("/api/auth/logout", null)).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task AnonymousMe_ReturnsJson401WithoutRedirect()
    {
        var response = await client.GetAsync("/api/auth/me");
        await AssertError(response, HttpStatusCode.Unauthorized);
        Assert.That(response.Headers.Location, Is.Null);
    }

    [Test]
    public async Task DeletedUser_CannotRestoreSession()
    {
        await Signup();
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.ExecuteDeleteAsync();
        await AssertError(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task SwitchingAccounts_InvalidatesThePreviousCookie()
    {
        using var other = app.CreateApiClient();
        var otherSignup = await other.PostAsJsonAsync("/api/auth/signup", new { username = "Avery", password = "password2" });
        var avery = await otherSignup.Content.ReadFromJsonAsync<JsonElement>();
        var original = await Signup();
        using var replay = app.CreateApiClient();
        replay.DefaultRequestHeaders.Add("Cookie", SessionCookie(original));
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "Avery", password = "password2" });
        await AssertIdentity(login, avery.GetProperty("id").GetString(), "Avery");
        await AssertIdentity(await client.GetAsync("/api/auth/me"), avery.GetProperty("id").GetString(), "Avery");
        await AssertError(await replay.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task OlderPasswordHash_IsUpgradedOnSuccessfulLogin()
    {
        string oldHash;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            var user = new User { Username = "River", NormalizedUsername = "RIVER" };
            var oldHasher = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions { IterationCount = 1000 }));
            user.PasswordHash = oldHasher.HashPassword(user, "password1");
            oldHash = user.PasswordHash;
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "River", password = "password1" });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var check = app.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.SingleAsync();
        Assert.That(saved.PasswordHash, Is.Not.EqualTo(oldHash));
        Assert.That(check.ServiceProvider.GetRequiredService<IPasswordHasher<User>>()
            .VerifyHashedPassword(saved, saved.PasswordHash, "password1"), Is.EqualTo(PasswordVerificationResult.Success));
    }

    private async Task<HttpResponseMessage> Signup()
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new { username = "River", password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return response;
    }

    private static string SessionCookie(HttpResponseMessage response) => response.Headers.GetValues("Set-Cookie")
        .Last(cookie => cookie.StartsWith("RouteWise.Session=", StringComparison.Ordinal)).Split(';')[0];

    private static async Task AssertIdentity(HttpResponseMessage response, string? id, string username)
    {
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "username" }));
            Assert.That(body.GetProperty("id").GetString(), Is.EqualTo(id));
            Assert.That(body.GetProperty("username").GetString(), Is.EqualTo(username));
        });
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string? message = null)
    {
        Assert.That(response.StatusCode, Is.EqualTo(status));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "message" }));
        Assert.That(body.GetProperty("message").GetString(), Is.Not.Empty);
        if (message is not null) Assert.That(body.GetProperty("message").GetString(), Is.EqualTo(message));
    }
}
