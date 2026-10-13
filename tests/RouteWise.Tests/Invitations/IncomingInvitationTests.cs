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

public sealed class IncomingInvitationTests
{
    private SignupApplication app = null!;
    private HttpClient owner = null!;
    private HttpClient recipient = null!;
    private string recipientId = null!;

    [SetUp]
    public async Task SetUp()
    {
        app = new SignupApplication();
        owner = app.CreateApiClient();
        recipient = app.CreateApiClient();
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Database.MigrateAsync();
        await Signup(owner, "River");
        recipientId = await Signup(recipient, "Avery");
    }

    [TearDown]
    public void TearDown()
    {
        owner.Dispose();
        recipient.Dispose();
        app.Dispose();
    }

    [Test]
    public async Task IncomingList_IncludesAllStatusesAndCurrentJourneySummaries()
    {
        var invitationIds = new List<string>();
        foreach (var status in new[] { InvitationStatus.Pending, InvitationStatus.Accepted, InvitationStatus.Declined })
        {
            var (journeyId, invitationId) = await Invite(recipientId);
            invitationIds.Add(invitationId);
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            (await db.Invitations.SingleAsync(i => i.Id == invitationId)).Status = status;
            await db.SaveChangesAsync();

            var renamed = await owner.PatchAsJsonAsync($"/api/journeys/{journeyId}",
                new { name = $"Updated {status}" });
            Assert.That(renamed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var response = await recipient.GetAsync("/api/invitations");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.EnumerateArray().Select(i => i.GetProperty("id").GetString()), Is.EquivalentTo(invitationIds));
        Assert.That(body.EnumerateArray().Select(i => i.GetProperty("status").GetString()),
            Is.EquivalentTo(new[] { "pending", "accepted", "declined" }));
        foreach (var item in body.EnumerateArray())
        {
            var status = item.GetProperty("status").GetString()!;
            var journey = item.GetProperty("journey");
            Assert.That(journey.GetProperty("name").GetString(),
                Is.EqualTo($"Updated {char.ToUpperInvariant(status[0])}{status[1..]}"));
            Assert.That(journey.GetProperty("start").GetProperty("name").GetString(), Is.EqualTo("Park"));
            Assert.That(journey.GetProperty("end").GetProperty("name").GetString(), Is.EqualTo("Cafe"));
            Assert.That(journey.GetProperty("totalDistanceMeters").GetDouble(), Is.EqualTo(100));
            Assert.That(journey.GetProperty("totalDurationSeconds").GetDouble(), Is.EqualTo(90));
            Assert.That(journey.GetProperty("locationCount").GetInt32(), Is.EqualTo(2));
            Assert.That(item.GetProperty("recipient").GetProperty("id").GetString(), Is.EqualTo(recipientId));
            foreach (var field in new[] { "sender", "recipient" })
                Assert.That(item.GetProperty(field).EnumerateObject().Select(p => p.Name),
                    Is.EquivalentTo(new[] { "id", "username" }));

            // Listing a summary does not change the invitation's access rules.
            var detail = await recipient.GetAsync($"/api/journeys/{journey.GetProperty("id").GetString()}");
            Assert.That(detail.StatusCode, Is.EqualTo(status == "accepted" ? HttpStatusCode.OK : HttpStatusCode.Forbidden));
        }
    }

    [Test]
    public async Task IncomingList_ExcludesOtherRecipientsAndSentInvitations()
    {
        var (_, ownInvitationId) = await Invite(recipientId);
        using var other = app.CreateApiClient();
        var otherId = await Signup(other, "Quinn");
        await Invite(otherId);

        var response = await recipient.GetAsync($"/api/invitations?recipientId={otherId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.EnumerateArray().Select(i => i.GetProperty("id").GetString()),
            Is.EqualTo(new[] { ownInvitationId }));

        var sentOnly = await owner.GetAsync("/api/invitations");
        Assert.That(sentOnly.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await sentOnly.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task IncomingList_NoInvitationsReturnsEmptyArray()
    {
        var response = await recipient.GetAsync("/api/invitations");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await response.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task IncomingList_AnonymousRequestReturns401()
    {
        using var anonymous = app.CreateApiClient();
        var response = await anonymous.GetAsync("/api/invitations");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.Location, Is.Null);
    }

    [Test]
    public async Task SharedJourney_AcceptThenRemoveRevokesAccess()
    {
        // Arrange: River creates a journey and invites Avery.
        var (journeyId, invitationId) = await Invite(recipientId);
        var accepted = await recipient.PatchAsJsonAsync($"/api/invitations/{invitationId}",
            new { status = "accepted" });
        Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Acceptance adds the journey to Shared and grants detail access.
        var sharedBefore = await recipient.GetAsync("/api/shared-journeys");
        Assert.That(sharedBefore.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var before = await sharedBefore.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(before.GetArrayLength(), Is.EqualTo(1));
        Assert.That(before[0].GetProperty("id").GetString(), Is.EqualTo(journeyId));
        var detailBefore = await recipient.GetAsync($"/api/journeys/{journeyId}");
        Assert.That(detailBefore.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act: remove the recipient's shared relationship.
        var removed = await recipient.DeleteAsync($"/api/shared-journeys/{journeyId}");
        Assert.That(removed.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Assert: neither the Shared list nor direct details remain accessible.
        var sharedAfter = await recipient.GetAsync("/api/shared-journeys");
        Assert.That(sharedAfter.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var after = await sharedAfter.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(after.GetArrayLength(), Is.Zero);
        var detailAfter = await recipient.GetAsync($"/api/journeys/{journeyId}");
        Assert.That(detailAfter.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // The owner's journey and invitation history remain intact.
        var ownerDetail = await owner.GetAsync($"/api/journeys/{journeyId}");
        Assert.That(ownerDetail.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        var invitation = await db.Invitations.SingleAsync(item => item.Id == invitationId);
        Assert.That(invitation.Status, Is.EqualTo(InvitationStatus.Accepted));
        Assert.That(invitation.RemovedFromShared, Is.True);
    }

    [Test]
    public async Task SharedJourney_RemovalDoesNotAffectOtherRecipients()
    {
        // Arrange: Avery and Quinn accept invitations to the same journey.
        var (journeyId, averyInvitationId) = await Invite(recipientId);
        var averyAccepted = await recipient.PatchAsJsonAsync($"/api/invitations/{averyInvitationId}",
            new { status = "accepted" });
        Assert.That(averyAccepted.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var quinn = app.CreateApiClient();
        var quinnId = await Signup(quinn, "Quinn");
        var quinnInvited = await owner.PostAsJsonAsync($"/api/journeys/{journeyId}/invitations",
            new { recipientId = quinnId });
        Assert.That(quinnInvited.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var invitation = await quinnInvited.Content.ReadFromJsonAsync<JsonElement>();
        var quinnInvitationId = invitation.GetProperty("id").GetString();
        var quinnAccepted = await quinn.PatchAsJsonAsync($"/api/invitations/{quinnInvitationId}",
            new { status = "accepted" });
        Assert.That(quinnAccepted.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Act: Avery removes their shared relationship.
        var removed = await recipient.DeleteAsync($"/api/shared-journeys/{journeyId}");
        Assert.That(removed.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Assert: Avery loses access while Quinn retains detail and list access.
        var averyDetail = await recipient.GetAsync($"/api/journeys/{journeyId}");
        Assert.That(averyDetail.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var quinnDetail = await quinn.GetAsync($"/api/journeys/{journeyId}");
        Assert.That(quinnDetail.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var shared = await quinn.GetAsync("/api/shared-journeys");
        Assert.That(shared.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var journeys = await shared.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(journeys.EnumerateArray().Select(journey => journey.GetProperty("id").GetString()),
            Is.EqualTo(new[] { journeyId }));

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        var averyRecord = await db.Invitations.SingleAsync(item => item.Id == averyInvitationId);
        var quinnRecord = await db.Invitations.SingleAsync(item => item.Id == quinnInvitationId);
        Assert.That(averyRecord.RemovedFromShared, Is.True);
        Assert.That(quinnRecord.RemovedFromShared, Is.False);
    }

    private async Task<(string JourneyId, string InvitationId)> Invite(string targetId)
    {
        var request = new CreateJourneyRequest("Park walk", null,
            new("walking", "distance", "open", new(
                [new("start", new("park", "Park", null, new(-33.86, 151.20))),
                 new("end", new("cafe", "Cafe", null, new(-33.87, 151.21)))],
                [new("leg", "start", "end", 100, 90,
                    [new(-33.86, 151.20), new(-33.87, 151.21)])], 100, 90)), null);
        var created = await owner.PostAsJsonAsync("/api/journeys", request);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var journeyId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var invited = await owner.PostAsJsonAsync($"/api/journeys/{journeyId}/invitations", new { recipientId = targetId });
        Assert.That(invited.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var invitationId = (await invited.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        return (journeyId, invitationId);
    }

    private static async Task<string> Signup(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new { username, password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }
}
