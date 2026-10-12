using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Data;
using RouteWise.Api.Models;
using RouteWise.Tests.Auth;

namespace RouteWise.Tests.Journeys;

public sealed class JourneyListTests
{
    private SignupApplication app = null!;
    private HttpClient client = null!;

    [SetUp]
    public async Task SetUp()
    {
        app = new SignupApplication();
        client = app.CreateApiClient();
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Database.MigrateAsync();
    }

    [TearDown]
    public void TearDown()
    {
        client.Dispose();
        app.Dispose();
    }

    [Test]
    public async Task OwnedList_ReturnsSummariesAndExcludesOtherAndSharedJourneys()
    {
        var ownerId = await Signup(client, "River");
        var firstId = await CreateJourney(client, "Morning walk");
        var secondId = await CreateJourney(client, "Afternoon walk");
        using var other = app.CreateApiClient();
        var otherId = await Signup(other, "Avery");
        var sharedId = await CreateJourney(other, "Shared walk");
        await CreateJourney(other, "Private walk");

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            db.Invitations.Add(new Invitation
            {
                JourneyId = sharedId, SenderId = otherId, RecipientId = ownerId,
                Status = InvitationStatus.Accepted
            });
            await db.SaveChangesAsync();
        }

        // A query parameter must not let the caller impersonate another owner.
        var response = await client.GetAsync($"/api/journeys?userId={otherId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        Assert.That(body.EnumerateArray().Select(item => item.GetProperty("id").GetString()),
            Is.EquivalentTo(new[] { firstId, secondId }));

        var summary = body.EnumerateArray().Single(item => item.GetProperty("id").GetString() == firstId);
        Assert.Multiple(() =>
        {
            Assert.That(summary.GetProperty("name").GetString(), Is.EqualTo("Morning walk"));
            Assert.That(summary.GetProperty("owner").GetProperty("id").GetString(), Is.EqualTo(ownerId));
            Assert.That(summary.GetProperty("owner").EnumerateObject().Select(p => p.Name),
                Is.EquivalentTo(new[] { "id", "username" }));
            Assert.That(summary.GetProperty("travelMode").GetString(), Is.EqualTo("walking"));
            Assert.That(summary.GetProperty("representativePlaceId").GetString(), Is.EqualTo("park"));
            Assert.That(summary.GetProperty("start").GetProperty("name").GetString(), Is.EqualTo("Park"));
            Assert.That(summary.GetProperty("end").GetProperty("name").GetString(), Is.EqualTo("Cafe"));
            Assert.That(summary.GetProperty("locationCount").GetInt32(), Is.EqualTo(2));
            Assert.That(summary.GetProperty("totalDistanceMeters").GetDouble(), Is.EqualTo(100));
            Assert.That(summary.GetProperty("totalDurationSeconds").GetDouble(), Is.EqualTo(90));
            Assert.That(summary.TryGetProperty("date", out _), Is.False);
            Assert.That(summary.TryGetProperty("plan", out _), Is.False);
            Assert.That(summary.TryGetProperty("annotations", out _), Is.False);
        });
    }

    [Test]
    public async Task UserWithoutJourneys_ReturnsEmptyArray()
    {
        await Signup(client, "River");
        var response = await client.GetAsync("/api/journeys");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task AnonymousUser_Returns401WithoutRedirect()
    {
        var response = await client.GetAsync("/api/journeys");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.Location, Is.Null);
    }

    private static async Task<string> Signup(HttpClient browser, string username)
    {
        var response = await browser.PostAsJsonAsync("/api/auth/signup",
            new { username, password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateJourney(HttpClient browser, string name)
    {
        var request = new CreateJourneyRequest(name, null,
            new("walking", "distance", "open", new(
                [new("start", new("park", "Park", null, new(-33.86, 151.20))),
                 new("end", new("cafe", "Cafe", null, new(-33.87, 151.21)))],
                [new("leg", "start", "end", 100, 90,
                    [new(-33.86, 151.20), new(-33.87, 151.21)])], 100, 90)), null);
        var response = await browser.PostAsJsonAsync("/api/journeys", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }
}
