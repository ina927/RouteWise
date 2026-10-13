using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Data;
using RouteWise.Api.Mapping;
using RouteWise.Api.Models;
using RouteWise.Tests.Auth;

namespace RouteWise.Tests.Journeys;

public sealed class JourneyDetailTests
{
    private SignupApplication app = null!;
    private HttpClient owner = null!;
    private string journeyId = null!;
    private string ownerId = null!;

    [SetUp]
    public async Task SetUp()
    {
        app = new SignupApplication();
        owner = app.CreateApiClient();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        await db.Database.MigrateAsync();
        ownerId = await Signup(owner, "River");
        var place = new PlaceDto("park", "Park", null, new(-33.86, 151.2));
        var request = new CreateJourneyRequest("Walk", "2026-10-10",
            new("walking", "distance", "returnToStart", new(
                [new("start", place), new("return", place)],
                [new("leg", "start", "return", 100, 90, [new(-33.86, 151.2), new(-33.87, 151.21)])], 100, 90)),
            [new("return", "Finish", "15:30"), new("start", "Begin", null)]);
        var journey = JourneyMapper.ToEntity(request, ownerId);
        journey.Points.Reverse();
        journeyId = journey.Id;
        db.Journeys.Add(journey);
        await db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown()
    {
        owner.Dispose();
        app.Dispose();
    }

    [Test]
    public async Task Owner_GetsStoredRouteAndOccurrenceMetadata()
    {
        var response = await owner.GetAsync($"/api/journeys/{journeyId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
            Assert.That(body.GetProperty("id").GetString(), Is.EqualTo(journeyId));
            Assert.That(body.GetProperty("access").GetString(), Is.EqualTo("owner"));
            Assert.That(body.GetProperty("date").GetString(), Is.EqualTo("2026-10-10"));
            Assert.That(body.GetProperty("owner").EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "username" }));
            var route = body.GetProperty("plan").GetProperty("route");
            Assert.That(route.GetProperty("points").EnumerateArray().Select(p => p.GetProperty("id").GetString()),
                Is.EqualTo(new[] { "start", "return" }));
            Assert.That(route.GetProperty("totalDistanceMeters").GetDouble(), Is.EqualTo(100));
            Assert.That(route.GetProperty("totalDurationSeconds").GetDouble(), Is.EqualTo(90));
            Assert.That(route.GetProperty("segments")[0].GetProperty("path")[1].GetProperty("lng").GetDouble(), Is.EqualTo(151.21));
            Assert.That(body.GetProperty("annotations")[0].GetProperty("note").GetString(), Is.EqualTo("Begin"));
            Assert.That(body.GetProperty("annotations")[1].GetProperty("plannedTime").GetString(), Is.EqualTo("15:30"));
        });
    }

    [TestCase(InvitationStatus.Accepted, false, HttpStatusCode.OK)]
    [TestCase(InvitationStatus.Pending, false, HttpStatusCode.Forbidden)]
    [TestCase(InvitationStatus.Declined, false, HttpStatusCode.Forbidden)]
    [TestCase(InvitationStatus.Accepted, true, HttpStatusCode.Forbidden)]
    public async Task RecipientAccess_RequiresActiveAcceptance(InvitationStatus status, bool removed, HttpStatusCode expected)
    {
        using var recipient = app.CreateApiClient();
        var recipientId = await Signup(recipient, "Avery");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            db.Invitations.Add(new Invitation { JourneyId = journeyId, SenderId = ownerId,
                RecipientId = recipientId, Status = status, RemovedFromShared = removed });
            await db.SaveChangesAsync();
        }
        var response = await recipient.GetAsync($"/api/journeys/{journeyId}");
        if (expected == HttpStatusCode.OK)
        {
            Assert.That(response.StatusCode, Is.EqualTo(expected));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.That(body.GetProperty("access").GetString(), Is.EqualTo("shared"));
            Assert.That(body.GetProperty("owner").GetProperty("id").GetString(), Is.EqualTo(ownerId));
        }
        else await AssertError(response, expected);
    }

    [Test]
    public async Task UnrelatedUser_CannotClaimOwnerIdentityInQuery()
    {
        using var other = app.CreateApiClient();
        await Signup(other, "Other");
        await AssertError(await other.GetAsync($"/api/journeys/{journeyId}?userId={ownerId}&access=owner"), HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task MissingJourney_Returns404()
    {
        await AssertError(await owner.GetAsync("/api/journeys/does-not-exist"), HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AnonymousRequest_Returns401WithoutRedirect()
    {
        using var anonymous = app.CreateApiClient();
        var response = await anonymous.GetAsync($"/api/journeys/{journeyId}");
        await AssertError(response, HttpStatusCode.Unauthorized);
        Assert.That(response.Headers.Location, Is.Null);
    }

    private static async Task<string> Signup(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new { username, password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        return user.GetProperty("id").GetString()!;
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.That(response.StatusCode, Is.EqualTo(status));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "message" }));
        Assert.That(body.GetProperty("message").GetString(), Is.Not.Empty);
    }
}
