using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RouteWise.Api.Data;
using RouteWise.Tests.Auth;

namespace RouteWise.Tests.Users;

public sealed class UserSearchTests
{
    private SignupApplication app = null!;
    private HttpClient client = null!;

    [SetUp]
    public async Task SetUp()
    {
        app = new SignupApplication();
        client = app.CreateApiClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        await db.Database.MigrateAsync();
    }

    [TearDown]
    public void TearDown()
    {
        client.Dispose();
        app.Dispose();
    }

    [TestCase("ave")]
    [TestCase("AVE")]
    [TestCase(" Ave ")]
    public async Task Search_MatchesUsernameAndReturnsOnlyPublicFields(string query)
    {
        await Signup(client, "River");
        using var other = app.CreateApiClient();
        var averyId = await Signup(other, "Avery");

        var response = await client.GetAsync($"/api/users?query={Uri.EscapeDataString(query)}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(results.GetArrayLength(), Is.EqualTo(1));
        var user = results[0];
        Assert.That(user.GetProperty("id").GetString(), Is.EqualTo(averyId));
        Assert.That(user.GetProperty("username").GetString(), Is.EqualTo("Avery"));
        Assert.That(user.EnumerateObject().Select(property => property.Name),
            Is.EquivalentTo(new[] { "id", "username" }));
    }

    [Test]
    public async Task Search_ExcludesCurrentUser()
    {
        await Signup(client, "River");
        var response = await client.GetAsync("/api/users?query=river");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(results.GetArrayLength(), Is.Zero);
    }

    [TestCase("/api/users")]
    [TestCase("/api/users?query=")]
    [TestCase("/api/users?query=%20%20")]
    [TestCase("/api/users?query=nomatch")]
    public async Task Search_EmptyOrUnmatchedQueryReturnsEmptyList(string url)
    {
        await Signup(client, "River");
        using var other = app.CreateApiClient();
        await Signup(other, "Avery");
        var response = await client.GetAsync(url);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var results = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(results.GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task Search_AnonymousUserReturns401()
    {
        var response = await client.GetAsync("/api/users?query=ave");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.Location, Is.Null);
    }

    private static async Task<string> Signup(HttpClient browser, string username)
    {
        var response = await browser.PostAsJsonAsync("/api/auth/signup",
            new { username, password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        return user.GetProperty("id").GetString()!;
    }
}
