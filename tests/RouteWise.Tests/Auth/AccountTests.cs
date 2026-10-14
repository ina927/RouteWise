using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RouteWise.Api.Data;
using RouteWise.Api.Models;

namespace RouteWise.Tests.Auth;

public sealed class AccountTests
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
    public async Task AnonymousDeletion_IsRejectedAndPreservesAccounts()
    {
        await Signup();
        using var anonymous = app.CreateApiClient();
        await AssertError(await anonymous.DeleteAsync("/api/account"), HttpStatusCode.Unauthorized);
        using var scope = app.Services.CreateScope();
        Assert.That(await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.CountAsync(), Is.EqualTo(1));
    }

    [TestCase(null)]
    [TestCase("null")]
    [TestCase("https://untrusted.example")]
    public async Task UntrustedOrigin_DoesNotDeleteOrSignOut(string? origin)
    {
        await Signup();
        client.DefaultRequestHeaders.Remove("Origin");
        if (origin is not null) client.DefaultRequestHeaders.Add("Origin", origin);
        await AssertError(await client.DeleteAsync("/api/account"), HttpStatusCode.Forbidden);
        Assert.That((await client.GetAsync("/api/auth/me")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = app.Services.CreateScope();
        Assert.That(await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task Delete_InvalidatesSessionsAndAllowsUsernameReuseWithoutOldAccess()
    {
        var signup = await Signup();
        var original = await signup.Content.ReadFromJsonAsync<JsonElement>();
        var oldCookie = signup.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        using var secondSession = app.CreateApiClient();
        var login = await secondSession.PostAsJsonAsync("/api/auth/login", new { username = "River", password = "password1" });
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var deletion = await client.DeleteAsync("/api/account");
        Assert.That(deletion.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await deletion.Content.ReadAsStringAsync(), Is.Empty);
        Assert.That(deletion.Headers.GetValues("Set-Cookie").Any(c => c.StartsWith("RouteWise.Session=;")), Is.True);
        using (var scope = app.Services.CreateScope())
            Assert.That(await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Users.CountAsync(), Is.Zero);
        await AssertError(await client.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
        await AssertError(await client.PostAsJsonAsync("/api/auth/login", new { username = "River", password = "password1" }),
            HttpStatusCode.Unauthorized);

        var replacement = await client.PostAsJsonAsync("/api/auth/signup", new { username = "river", password = "new-password" });
        Assert.That(replacement.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var recreated = await replacement.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(recreated.GetProperty("id").GetString(), Is.Not.EqualTo(original.GetProperty("id").GetString()));

        // Even before another session has noticed deletion, username reuse cannot revive it.
        await AssertError(await secondSession.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
        using var replay = app.CreateApiClient();
        replay.DefaultRequestHeaders.Add("Cookie", oldCookie);
        await AssertError(await replay.GetAsync("/api/auth/me"), HttpStatusCode.Unauthorized);
        await AssertError(await replay.DeleteAsync("/api/account"), HttpStatusCode.Unauthorized);
        Assert.That((await client.GetAsync("/api/auth/me")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Delete_CascadesOwnedDataAndPreservesOtherOwnersAndRecipients()
    {
        await Signup();
        string otherUserId;
        string otherJourneyId;
        string retainedInvitationId;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            var river = await db.Users.SingleAsync();
            var avery = new User { Username = "Avery", NormalizedUsername = "AVERY", PasswordHash = "test-placeholder" };
            var quinn = new User { Username = "Quinn", NormalizedUsername = "QUINN", PasswordHash = "test-placeholder" };
            var owned = JourneyFor(river);
            var otherOwned = JourneyFor(avery);
            db.Journeys.AddRange(owned, otherOwned);
            var retained = new Invitation { Journey = otherOwned, Sender = avery, Recipient = quinn, Status = InvitationStatus.Accepted };
            db.Invitations.AddRange(
                new Invitation { Journey = owned, Sender = river, Recipient = avery, Status = InvitationStatus.Pending },
                new Invitation { Journey = owned, Sender = river, Recipient = quinn, Status = InvitationStatus.Declined },
                new Invitation { Journey = otherOwned, Sender = avery, Recipient = river, Status = InvitationStatus.Accepted },
                retained);
            await db.SaveChangesAsync();
            otherUserId = avery.Id;
            otherJourneyId = otherOwned.Id;
            retainedInvitationId = retained.Id;
        }

        // An arbitrary userId supplied by a caller must never select the deleted account.
        Assert.That((await client.DeleteAsync($"/api/account?userId={otherUserId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));
        using var check = app.Services.CreateScope();
        var saved = check.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        Assert.Multiple(() =>
        {
            Assert.That(saved.Users.Select(u => u.Username).ToArray(), Is.EquivalentTo(new[] { "Avery", "Quinn" }));
            Assert.That(saved.Journeys.Single().Id, Is.EqualTo(otherJourneyId));
            Assert.That(saved.JourneyPoints.Count(), Is.EqualTo(2));
            Assert.That(saved.JourneyPoints.All(p => p.JourneyId == otherJourneyId), Is.True);
            Assert.That(saved.JourneySegments.Single().JourneyId, Is.EqualTo(otherJourneyId));
            Assert.That(saved.Invitations.Single().Id, Is.EqualTo(retainedInvitationId));
        });
    }

    private async Task<HttpResponseMessage> Signup()
    {
        var response = await client.PostAsJsonAsync("/api/auth/signup", new { username = "River", password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return response;
    }

    private static Journey JourneyFor(User owner) => new()
    {
        Owner = owner, Name = "Walk", TravelMode = TravelMode.Walking,
        Objective = OptimisationObjective.Distance, EndMode = EndMode.Open,
        Points =
        [
            new() { Id = "start", Position = 0, PlaceId = "park", Name = "Park", Latitude = -33.86, Longitude = 151.2 },
            new() { Id = "end", Position = 1, PlaceId = "museum", Name = "Museum", Latitude = -33.87, Longitude = 151.21 }
        ],
        Segments =
        [
            new() { Id = "leg", FromPointId = "start", ToPointId = "end", DistanceMeters = 100, DurationSeconds = 90,
                Path = [new() { Lat = -33.86, Lng = 151.2 }, new() { Lat = -33.87, Lng = 151.21 }] }
        ]
    };

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.That(response.StatusCode, Is.EqualTo(status));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.EnumerateObject().Select(p => p.Name), Is.EqualTo(new[] { "message" }));
        Assert.That(body.GetProperty("message").GetString(), Is.Not.Empty);
    }
}
