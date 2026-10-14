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

namespace RouteWise.Tests.Invitations;

public sealed class JourneyInvitationListTests
{
    private SignupApplication app = null!;
    private HttpClient owner = null!;
    private string journeyId = null!;
    private string Url => $"/api/journeys/{journeyId}/invitations";

    [SetUp]
    public async Task SetUp()
    {
        app = new SignupApplication();
        owner = app.CreateApiClient();
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Database.MigrateAsync();
        await Signup(owner, "River");
        journeyId = await CreateJourney();
    }

    [TearDown]
    public void TearDown()
    {
        owner.Dispose();
        app.Dispose();
    }

    [Test]
    public async Task OwnerList_IncludesAllStatusesAndRemovedHistoryForOnlyThisJourney()
    {
        var expectedIds = new List<string>();
        var statuses = new[] { InvitationStatus.Pending, InvitationStatus.Accepted,
            InvitationStatus.Declined, InvitationStatus.Accepted };
        for (var index = 0; index < statuses.Length; index++)
        {
            using var recipient = app.CreateApiClient();
            var recipientId = await Signup(recipient, $"Guest{index}");
            var invitationId = await Invite(journeyId, recipientId);
            expectedIds.Add(invitationId);
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            var invitation = await db.Invitations.SingleAsync(i => i.Id == invitationId);
            invitation.Status = statuses[index];
            invitation.RemovedFromShared = index == 3;
            await db.SaveChangesAsync();

            // Same owner and recipient, but a different journey: must be excluded.
            if (index == 0)
                await Invite(await CreateJourney(), recipientId);
        }

        var renamed = await owner.PatchAsJsonAsync($"/api/journeys/{journeyId}", new { name = "Updated walk" });
        Assert.That(renamed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var response = await owner.GetAsync(Url);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.EnumerateArray().Select(item => item.GetProperty("id").GetString()),
            Is.EquivalentTo(expectedIds));
        Assert.That(body.EnumerateArray().Select(item => item.GetProperty("status").GetString()),
            Is.EquivalentTo(new[] { "pending", "accepted", "declined", "removed" }));
        foreach (var item in body.EnumerateArray())
        {
            var journey = item.GetProperty("journey");
            Assert.That(journey.GetProperty("id").GetString(), Is.EqualTo(journeyId));
            Assert.That(journey.GetProperty("name").GetString(), Is.EqualTo("Updated walk"));
            Assert.That(journey.GetProperty("locationCount").GetInt32(), Is.EqualTo(2));
            Assert.That(journey.GetProperty("totalDistanceMeters").GetDouble(), Is.EqualTo(100));
            foreach (var field in new[] { "sender", "recipient" })
                Assert.That(item.GetProperty(field).EnumerateObject().Select(property => property.Name),
                    Is.EquivalentTo(new[] { "id", "username" }));
        }
    }

    [Test]
    public async Task OwnerList_NoInvitationsReturnsEmptyArray()
    {
        var response = await owner.GetAsync(Url);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength(), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NonOwner_CannotListEvenWithAcceptedAccess(bool shared)
    {
        using var other = app.CreateApiClient();
        var otherId = await Signup(other, "Avery");
        if (shared)
        {
            var invitationId = await Invite(journeyId, otherId);
            var accepted = await other.PatchAsJsonAsync($"/api/invitations/{invitationId}", new { status = "accepted" });
            Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await other.GetAsync($"/api/journeys/{journeyId}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        await AssertError(await other.GetAsync(Url), HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task MissingJourney_Returns404()
    {
        await AssertError(await owner.GetAsync("/api/journeys/missing/invitations"), HttpStatusCode.NotFound);
    }

    [Test]
    public async Task AnonymousUser_Returns401WithoutRedirect()
    {
        using var anonymous = app.CreateApiClient();
        var response = await anonymous.GetAsync(Url);
        await AssertError(response, HttpStatusCode.Unauthorized);
        Assert.That(response.Headers.Location, Is.Null);
    }

    private async Task<string> CreateJourney()
    {
        var request = new CreateJourneyRequest("Park walk", null,
            new("walking", "distance", "open", new(
                [new("start", new("park", "Park", null, new(-33.86, 151.20))),
                 new("end", new("cafe", "Cafe", null, new(-33.87, 151.21)))],
                [new("leg", "start", "end", 100, 90,
                    [new(-33.86, 151.20), new(-33.87, 151.21)])], 100, 90)), null);
        var response = await owner.PostAsJsonAsync("/api/journeys", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<string> Invite(string targetJourneyId, string recipientId)
    {
        var response = await owner.PostAsJsonAsync($"/api/journeys/{targetJourneyId}/invitations", new { recipientId });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task<string> Signup(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new { username, password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.That(response.StatusCode, Is.EqualTo(expected));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("message").GetString(), Is.Not.Null.And.Not.Empty);
    }
}
