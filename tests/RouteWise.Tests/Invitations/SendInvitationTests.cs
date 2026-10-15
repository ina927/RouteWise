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

public sealed class SendInvitationTests
{
    private SignupApplication app = null!;
    private HttpClient owner = null!;
    private HttpClient recipient = null!;
    private string ownerId = null!;
    private string recipientId = null!;
    private string journeyId = null!;
    private string Url => $"/api/journeys/{journeyId}/invitations";

    [SetUp]
    public async Task SetUp()
    {
        app = new SignupApplication();
        owner = app.CreateApiClient();
        recipient = app.CreateApiClient();
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Database.MigrateAsync();
        ownerId = await Signup(owner, "River");
        recipientId = await Signup(recipient, "Avery");
        var request = new CreateJourneyRequest("Park walk", null,
            new("walking", "distance", "open", new(
                [new("start", new("park", "Park", null, new(-33.86, 151.20))),
                 new("end", new("cafe", "Cafe", null, new(-33.87, 151.21)))],
                [new("leg", "start", "end", 100, 90,
                    [new(-33.86, 151.20), new(-33.87, 151.21)])], 100, 90)), null);
        var created = await owner.PostAsJsonAsync("/api/journeys", request);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        journeyId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    [TearDown]
    public void TearDown()
    {
        owner.Dispose();
        recipient.Dispose();
        app.Dispose();
    }

    [Test]
    public async Task Owner_CreatesPendingInvitationWithoutGrantingAccess()
    {
        // Extra client-supplied fields must not choose the sender or grant acceptance.
        var response = await owner.PostAsJsonAsync(Url,
            new { recipientId, senderId = recipientId, status = "accepted" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("status").GetString(), Is.EqualTo("pending"));
        Assert.That(body.GetProperty("sender").GetProperty("id").GetString(), Is.EqualTo(ownerId));
        Assert.That(body.GetProperty("recipient").GetProperty("id").GetString(), Is.EqualTo(recipientId));
        foreach (var field in new[] { "sender", "recipient" })
            Assert.That(body.GetProperty(field).EnumerateObject().Select(p => p.Name),
                Is.EquivalentTo(new[] { "id", "username" }));
        var journey = body.GetProperty("journey");
        Assert.That(journey.GetProperty("id").GetString(), Is.EqualTo(journeyId));
        Assert.That(journey.GetProperty("name").GetString(), Is.EqualTo("Park walk"));
        Assert.That(journey.GetProperty("totalDistanceMeters").GetDouble(), Is.EqualTo(100));

        using var scope = app.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Invitations.SingleAsync();
        Assert.That(stored.Id, Is.EqualTo(body.GetProperty("id").GetString()));
        Assert.That(stored.SenderId, Is.EqualTo(ownerId));
        Assert.That(stored.RecipientId, Is.EqualTo(recipientId));
        Assert.That(stored.JourneyId, Is.EqualTo(journeyId));
        Assert.That(stored.Status, Is.EqualTo(InvitationStatus.Pending));
        Assert.That(stored.RemovedFromShared, Is.False);
        Assert.That((await recipient.GetAsync($"/api/journeys/{journeyId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [TestCase(null, HttpStatusCode.BadRequest)]
    [TestCase("", HttpStatusCode.BadRequest)]
    [TestCase("   ", HttpStatusCode.BadRequest)]
    [TestCase("missing-user", HttpStatusCode.NotFound)]
    public async Task InvalidRecipient_SavesNothing(string? target, HttpStatusCode status)
    {
        await AssertError(await owner.PostAsJsonAsync(Url, new { recipientId = target }), status);
        await AssertCount(0);
    }

    [Test]
    public async Task SelfInvitation_SavesNothing()
    {
        await AssertError(await owner.PostAsJsonAsync(Url, new { recipientId = ownerId }), HttpStatusCode.BadRequest);
        await AssertCount(0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NonOwner_CannotSendEvenWithSharedAccess(bool shared)
    {
        if (shared)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            db.Invitations.Add(new Invitation { JourneyId = journeyId, SenderId = ownerId,
                RecipientId = recipientId, Status = InvitationStatus.Accepted });
            await db.SaveChangesAsync();
            Assert.That((await recipient.GetAsync($"/api/journeys/{journeyId}")).StatusCode,
                Is.EqualTo(HttpStatusCode.OK));
        }
        await AssertError(await recipient.PostAsJsonAsync(Url, new { recipientId = ownerId }), HttpStatusCode.Forbidden);
        await AssertCount(shared ? 1 : 0);
    }

    [Test]
    public async Task AnonymousRequest_IsRejected()
    {
        using var anonymous = app.CreateApiClient();
        var response = await anonymous.PostAsJsonAsync(Url, new { recipientId });
        await AssertError(response, HttpStatusCode.Unauthorized);
        Assert.That(response.Headers.Location, Is.Null);
        await AssertCount(0);
    }

    [Test]
    public async Task MissingJourney_IsRejected()
    {
        await AssertError(await owner.PostAsJsonAsync("/api/journeys/missing/invitations", new { recipientId }),
            HttpStatusCode.NotFound);
        await AssertCount(0);
    }

    [TestCase(InvitationStatus.Pending)]
    [TestCase(InvitationStatus.Accepted)]
    [TestCase(InvitationStatus.Declined)]
    public async Task ExistingActiveOrDeclinedInvitation_ReturnsConflict(InvitationStatus status)
    {
        var first = await owner.PostAsJsonAsync(Url, new { recipientId });
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            (await db.Invitations.SingleAsync()).Status = status;
            await db.SaveChangesAsync();
        }
        await AssertError(await owner.PostAsJsonAsync(Url, new { recipientId }), HttpStatusCode.Conflict);
        await AssertCount(1);
        using var checkScope = app.Services.CreateScope();
        var stored = await checkScope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Invitations.SingleAsync();
        Assert.That(stored.Status, Is.EqualTo(status));
    }

    [TestCase("accepted", HttpStatusCode.OK)]
    [TestCase("declined", HttpStatusCode.Forbidden)]
    public async Task RemovedInvitation_CanBeReusedWithoutAffectingOtherRecipients(
        string responseStatus, HttpStatusCode expectedAccess)
    {
        using var other = app.CreateApiClient();
        var otherId = await Signup(other, "Quinn");
        var otherSent = await owner.PostAsJsonAsync(Url, new { recipientId = otherId });
        Assert.That(otherSent.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var otherInvitationId = (await otherSent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        Assert.That((await other.PatchAsJsonAsync($"/api/invitations/{otherInvitationId}",
            new { status = "accepted" })).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var invitationId = await CreateRemovedInvitation();
        var list = await owner.GetFromJsonAsync<JsonElement>(Url);
        var removed = list.EnumerateArray().Single(item => item.GetProperty("id").GetString() == invitationId);
        Assert.That(removed.GetProperty("status").GetString(), Is.EqualTo("removed"));
        var incoming = await recipient.GetFromJsonAsync<JsonElement>("/api/invitations");
        Assert.That(incoming[0].GetProperty("status").GetString(), Is.EqualTo("removed"));
        await AssertError(await recipient.PatchAsJsonAsync($"/api/invitations/{invitationId}",
            new { status = "accepted" }), HttpStatusCode.Conflict);

        var resent = await owner.PostAsJsonAsync(Url, new { recipientId });
        Assert.That(resent.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var pending = await resent.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(pending.GetProperty("id").GetString(), Is.EqualTo(invitationId));
        Assert.That(pending.GetProperty("status").GetString(), Is.EqualTo("pending"));
        await AssertCount(2);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            var stored = await db.Invitations.SingleAsync(item => item.Id == invitationId);
            Assert.That(stored.Status, Is.EqualTo(InvitationStatus.Pending));
            Assert.That(stored.RemovedFromShared, Is.False);
            var unchanged = await db.Invitations.SingleAsync(item => item.Id == otherInvitationId);
            Assert.That(unchanged.Status, Is.EqualTo(InvitationStatus.Accepted));
            Assert.That(unchanged.RemovedFromShared, Is.False);
        }
        incoming = await recipient.GetFromJsonAsync<JsonElement>("/api/invitations");
        Assert.That(incoming.GetArrayLength(), Is.EqualTo(1));
        Assert.That(incoming[0].GetProperty("status").GetString(), Is.EqualTo("pending"));
        list = await owner.GetFromJsonAsync<JsonElement>(Url);
        Assert.That(list.EnumerateArray().Single(item => item.GetProperty("id").GetString() == invitationId)
            .GetProperty("status").GetString(), Is.EqualTo("pending"));
        Assert.That((await recipient.GetAsync($"/api/journeys/{journeyId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await recipient.GetFromJsonAsync<JsonElement>("/api/shared-journeys")).GetArrayLength(), Is.Zero);
        Assert.That((await other.GetAsync($"/api/journeys/{journeyId}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        await AssertError(await owner.PostAsJsonAsync(Url, new { recipientId }), HttpStatusCode.Conflict);

        var answered = await recipient.PatchAsJsonAsync($"/api/invitations/{invitationId}", new { status = responseStatus });
        Assert.That(answered.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await answered.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString(),
            Is.EqualTo(responseStatus));
        Assert.That((await recipient.GetAsync($"/api/journeys/{journeyId}")).StatusCode, Is.EqualTo(expectedAccess));
        Assert.That((await recipient.GetFromJsonAsync<JsonElement>("/api/shared-journeys")).GetArrayLength(),
            Is.EqualTo(responseStatus == "accepted" ? 1 : 0));
        Assert.That((await recipient.PatchAsJsonAsync($"/api/journeys/{journeyId}", new { name = "Changed" })).StatusCode,
            Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await other.GetAsync($"/api/journeys/{journeyId}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await other.GetFromJsonAsync<JsonElement>("/api/shared-journeys")).GetArrayLength(), Is.EqualTo(1));
        await AssertError(await owner.PostAsJsonAsync(Url, new { recipientId }), HttpStatusCode.Conflict);
        await AssertCount(2);
    }

    [Test]
    public async Task RemovedInvitation_CompetingReinvitesReuseOneRecord()
    {
        var invitationId = await CreateRemovedInvitation();
        var responses = await Task.WhenAll(
            owner.PostAsJsonAsync(Url, new { recipientId }),
            owner.PostAsJsonAsync(Url, new { recipientId }));
        Assert.That(responses.Select(response => response.StatusCode),
            Is.EquivalentTo(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
        var winner = await responses.Single(response => response.StatusCode == HttpStatusCode.Created)
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(winner.GetProperty("id").GetString(), Is.EqualTo(invitationId));
        Assert.That(winner.GetProperty("status").GetString(), Is.EqualTo("pending"));
        await AssertCount(1);
    }

    [Test]
    public async Task RemovedInvitation_CannotBeReinvitedByNonOwner()
    {
        var invitationId = await CreateRemovedInvitation();
        using var other = app.CreateApiClient();
        await Signup(other, "Quinn");
        await AssertError(await recipient.PostAsJsonAsync(Url, new { recipientId }), HttpStatusCode.Forbidden);
        await AssertError(await other.PostAsJsonAsync(Url, new { recipientId }), HttpStatusCode.Forbidden);
        using var anonymous = app.CreateApiClient();
        await AssertError(await anonymous.PostAsJsonAsync(Url, new { recipientId }), HttpStatusCode.Unauthorized);
        using var scope = app.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Invitations.SingleAsync();
        Assert.That(stored.Id, Is.EqualTo(invitationId));
        Assert.That(stored.Status, Is.EqualTo(InvitationStatus.Accepted));
        Assert.That(stored.RemovedFromShared, Is.True);
    }

    private async Task<string> CreateRemovedInvitation()
    {
        var sent = await owner.PostAsJsonAsync(Url, new { recipientId });
        Assert.That(sent.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var invitationId = (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        Assert.That((await recipient.PatchAsJsonAsync($"/api/invitations/{invitationId}",
            new { status = "accepted" })).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await recipient.DeleteAsync($"/api/shared-journeys/{journeyId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
        return invitationId;
    }

    private async Task AssertCount(int expected)
    {
        using var scope = app.Services.CreateScope();
        Assert.That(await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Invitations.CountAsync(),
            Is.EqualTo(expected));
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.That(response.StatusCode, Is.EqualTo(status));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("message").GetString(), Is.Not.Null.And.Not.Empty);
    }

    private static async Task<string> Signup(HttpClient client, string username)
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new { username, password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }
}
