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

public sealed class RespondToInvitationTests
{
    private SignupApplication app = null!;
    private HttpClient owner = null!;
    private HttpClient recipient = null!;
    private string journeyId = null!;
    private string invitationId = null!;
    private string Url => $"/api/invitations/{invitationId}";

    [SetUp]
    public async Task SetUp()
    {
        app = new SignupApplication();
        owner = app.CreateApiClient();
        recipient = app.CreateApiClient();
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Database.MigrateAsync();
        await Signup(owner, "River");
        var recipientId = await Signup(recipient, "Avery");
        var request = new CreateJourneyRequest("Park walk", null,
            new("walking", "distance", "open", new(
                [new("start", new("park", "Park", null, new(-33.86, 151.20))),
                 new("end", new("cafe", "Cafe", null, new(-33.87, 151.21)))],
                [new("leg", "start", "end", 100, 90,
                    [new(-33.86, 151.20), new(-33.87, 151.21)])], 100, 90)), null);
        var created = await owner.PostAsJsonAsync("/api/journeys", request);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        journeyId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var sent = await owner.PostAsJsonAsync($"/api/journeys/{journeyId}/invitations", new { recipientId });
        Assert.That(sent.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        invitationId = (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    [TearDown]
    public void TearDown()
    {
        owner.Dispose();
        recipient.Dispose();
        app.Dispose();
    }

    [TestCase("accepted", InvitationStatus.Accepted, HttpStatusCode.OK)]
    [TestCase("declined", InvitationStatus.Declined, HttpStatusCode.Forbidden)]
    public async Task RecipientResponse_PersistsAndControlsAccess(
        string status, InvitationStatus storedStatus, HttpStatusCode access)
    {
        Assert.That((await recipient.GetAsync($"/api/journeys/{journeyId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.Forbidden));
        var response = await recipient.PatchAsJsonAsync(Url, new { status });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("id").GetString(), Is.EqualTo(invitationId));
        Assert.That(body.GetProperty("status").GetString(), Is.EqualTo(status));
        Assert.That(body.GetProperty("journey").GetProperty("id").GetString(), Is.EqualTo(journeyId));
        await AssertStoredStatus(storedStatus);

        var incoming = await recipient.GetAsync("/api/invitations");
        Assert.That(incoming.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var list = await incoming.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(list[0].GetProperty("status").GetString(), Is.EqualTo(status));

        var detail = await recipient.GetAsync($"/api/journeys/{journeyId}");
        Assert.That(detail.StatusCode, Is.EqualTo(access));
        if (status == "accepted")
        {
            Assert.That((await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access").GetString(),
                Is.EqualTo("shared"));
            // Acceptance grants reading, not ownership or editing.
            var rename = await recipient.PatchAsJsonAsync($"/api/journeys/{journeyId}", new { name = "Changed" });
            Assert.That(rename.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }
        var ownerDetail = await owner.GetAsync($"/api/journeys/{journeyId}");
        Assert.That(ownerDetail.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await ownerDetail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString(),
            Is.EqualTo("Park walk"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("pending")]
    [TestCase("Accepted")]
    [TestCase(" accepted ")]
    [TestCase("unknown")]
    public async Task InvalidStatus_DoesNotChangeInvitation(string? status)
    {
        await AssertError(await recipient.PatchAsJsonAsync(Url, new { status }), HttpStatusCode.BadRequest);
        await AssertStoredStatus(InvitationStatus.Pending);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SenderAndUnrelatedUser_CannotRespond(bool useSender)
    {
        using var other = app.CreateApiClient();
        await Signup(other, "Quinn");
        var caller = useSender ? owner : other;
        await AssertError(await caller.PatchAsJsonAsync(Url, new { status = "accepted" }), HttpStatusCode.Forbidden);
        await AssertStoredStatus(InvitationStatus.Pending);
    }

    [TestCase("accepted", "accepted")]
    [TestCase("accepted", "declined")]
    [TestCase("declined", "accepted")]
    [TestCase("declined", "declined")]
    public async Task AnsweredInvitation_CannotBeAnsweredAgain(string first, string second)
    {
        Assert.That((await recipient.PatchAsJsonAsync(Url, new { status = first })).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));
        await AssertError(await recipient.PatchAsJsonAsync(Url, new { status = second }), HttpStatusCode.Conflict);
        await AssertStoredStatus(first == "accepted" ? InvitationStatus.Accepted : InvitationStatus.Declined);
    }

    [Test]
    public async Task AnonymousUser_CannotRespond()
    {
        using var anonymous = app.CreateApiClient();
        var response = await anonymous.PatchAsJsonAsync(Url, new { status = "accepted" });
        await AssertError(response, HttpStatusCode.Unauthorized);
        Assert.That(response.Headers.Location, Is.Null);
        await AssertStoredStatus(InvitationStatus.Pending);
    }

    [Test]
    public async Task MissingInvitation_Returns404()
    {
        await AssertError(await recipient.PatchAsJsonAsync("/api/invitations/missing",
            new { status = "accepted" }), HttpStatusCode.NotFound);
        await AssertStoredStatus(InvitationStatus.Pending);
    }

    [Test]
    public async Task CompetingResponses_OnlyOneSucceeds()
    {
        var responses = await Task.WhenAll(
            recipient.PatchAsJsonAsync(Url, new { status = "accepted" }),
            recipient.PatchAsJsonAsync(Url, new { status = "declined" }));
        Assert.That(responses.Select(response => response.StatusCode),
            Is.EquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var winner = await responses.Single(response => response.StatusCode == HttpStatusCode.OK)
            .Content.ReadFromJsonAsync<JsonElement>();
        await AssertStoredStatus(winner.GetProperty("status").GetString() == "accepted"
            ? InvitationStatus.Accepted : InvitationStatus.Declined);
    }

    private async Task AssertStoredStatus(InvitationStatus expected)
    {
        using var scope = app.Services.CreateScope();
        var invitation = await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Invitations.SingleAsync();
        Assert.That(invitation.Id, Is.EqualTo(invitationId));
        Assert.That(invitation.Status, Is.EqualTo(expected));
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.That(response.StatusCode, Is.EqualTo(expected));
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
