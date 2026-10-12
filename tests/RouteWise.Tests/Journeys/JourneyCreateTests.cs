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

public sealed class JourneyCreateTests
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

    [Test]
    public async Task CreateJourney_SavesRouteAndCanBeReadBack()
    {
        // Arrange: sign up through the API, which also logs this client in.
        var signup = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "River", password = "password1" });
        Assert.That(signup.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var user = await signup.Content.ReadFromJsonAsync<JsonElement>();

        // Act: submit a journey through the same HTTP boundary as the frontend.
        var created = await client.PostAsJsonAsync("/api/journeys", ValidRequest());

        // Assert: the API returns the saved journey and its new address.
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetString();
        Assert.Multiple(() =>
        {
            Assert.That(id, Is.Not.Null.And.Not.Empty);
            Assert.That(created.Headers.Location?.OriginalString, Is.EqualTo($"/api/journeys/{id}"));
            Assert.That(body.GetProperty("owner").GetProperty("id").GetString(),
                Is.EqualTo(user.GetProperty("id").GetString()));
            Assert.That(body.GetProperty("name").GetString(), Is.EqualTo("Park walk"));
            Assert.That(body.GetProperty("date").GetString(), Is.EqualTo("2026-10-11"));
            Assert.That(body.GetProperty("access").GetString(), Is.EqualTo("owner"));
            Assert.That(body.GetProperty("plan").GetProperty("route").GetProperty("totalDistanceMeters").GetDouble(), Is.EqualTo(100));
            Assert.That(body.GetProperty("annotations")[0].GetProperty("note").GetString(), Is.EqualTo("Meet here"));
        });

        // A separate HTTP request must load the same information from storage.
        var loaded = await client.GetAsync(created.Headers.Location);
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var loadedBody = await loaded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(loadedBody.GetRawText(), Is.EqualTo(body.GetRawText()));

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        Assert.That(await db.Journeys.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.JourneyPoints.CountAsync(), Is.EqualTo(2));
        Assert.That(await db.JourneySegments.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task InvalidJourney_Returns400AndSavesNothing()
    {
        var signup = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "River", password = "password1" });
        Assert.That(signup.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var response = await client.PostAsJsonAsync("/api/journeys",
            ValidRequest() with { Name = " " });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("message").GetString(), Is.Not.Null.And.Not.Empty);
        await AssertNoJourneySaved();
    }

    [Test]
    public async Task AnonymousUser_Returns401AndSavesNothing()
    {
        var response = await client.PostAsJsonAsync("/api/journeys", ValidRequest());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.Location, Is.Null);
        await AssertNoJourneySaved();
    }

    [Test]
    public async Task RenameJourney_SavesNewNameAndPreservesOtherData()
    {
        // Arrange: log in and create a journey.
        var signup = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "River", password = "password1" });
        Assert.That(signup.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await client.PostAsJsonAsync("/api/journeys", ValidRequest());
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetString();

        // Act: rename the journey, including spaces to check trimming.
        var renamed = await client.PatchAsJsonAsync($"/api/journeys/{id}",
            new { name = "  Sunday walk  " });
        Assert.That(renamed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var renameBody = await renamed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(renameBody.GetProperty("name").GetString(), Is.EqualTo("Sunday walk"));

        // Assert: a separate request must retrieve the saved name.
        var loaded = await client.GetAsync($"/api/journeys/{id}");
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await loaded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(saved.GetProperty("name").GetString(), Is.EqualTo("Sunday walk"));

        foreach (var field in new[] { "id", "owner", "date", "plan", "annotations" })
        {
            Assert.That(saved.GetProperty(field).GetRawText(),
                Is.EqualTo(original.GetProperty(field).GetRawText()),
                $"{field} should not change when renaming.");
        }
    }

    [Test]
    public async Task RenameJourney_OtherUserCannotChangeName()
    {
        // Arrange: River creates a journey in their own session.
        var ownerSignup = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "River", password = "password1" });
        Assert.That(ownerSignup.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await client.PostAsJsonAsync("/api/journeys", ValidRequest());
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetString();

        // A separate client keeps Avery's login separate from River's.
        using var otherClient = app.CreateApiClient();
        var otherSignup = await otherClient.PostAsJsonAsync("/api/auth/signup",
            new { username = "Avery", password = "password2" });
        Assert.That(otherSignup.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        // Act: Avery attempts to rename River's journey.
        var response = await otherClient.PatchAsJsonAsync($"/api/journeys/{id}",
            new { name = "Changed by Avery" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        // Assert: River still reads the original name from storage.
        var loaded = await client.GetAsync($"/api/journeys/{id}");
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await loaded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(saved.GetProperty("name").GetString(),
            Is.EqualTo(original.GetProperty("name").GetString()));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public async Task RenameJourney_InvalidNameKeepsOriginal(string? name)
    {
        // Arrange: create an account and a journey.
        var signup = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "River", password = "password1" });
        Assert.That(signup.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await client.PostAsJsonAsync("/api/journeys", ValidRequest());
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = original.GetProperty("id").GetString();

        // Act: submit an empty, whitespace-only, or null name.
        var response = await client.PatchAsJsonAsync($"/api/journeys/{id}", new { name });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("message").GetString(), Is.EqualTo("Journey name is required."));

        // Assert: the failed update leaves the whole stored journey unchanged.
        var loaded = await client.GetAsync($"/api/journeys/{id}");
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await loaded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(saved.GetRawText(), Is.EqualTo(original.GetRawText()));
    }

    [Test]
    public async Task RenameJourney_AnonymousUserReturns401()
    {
        // Authorization must reject the request before looking up the journey.
        var response = await client.PatchAsJsonAsync("/api/journeys/unknown-journey",
            new { name = "Sunday walk" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.Location, Is.Null);
    }

    [Test]
    public async Task RenameJourney_MissingJourneyReturns404()
    {
        var signup = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "River", password = "password1" });
        Assert.That(signup.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var response = await client.PatchAsJsonAsync("/api/journeys/unknown-journey",
            new { name = "Sunday walk" });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("message").GetString(), Is.EqualTo("Journey not found."));
    }

    [Test]
    public async Task DeleteJourney_CleansUpRelatedDataAndPreservesOtherJourneys()
    {
        var ownerId = await SignupForDelete(client, "River");
        using var other = app.CreateApiClient();
        var otherId = await SignupForDelete(other, "Avery");
        var deletedId = await CreateForDelete(client);
        var retainedOwnerId = await CreateForDelete(client);
        var retainedOtherId = await CreateForDelete(other);

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            db.Invitations.AddRange(
                new Invitation { JourneyId = deletedId, SenderId = ownerId,
                    RecipientId = otherId, Status = InvitationStatus.Accepted },
                new Invitation { JourneyId = retainedOwnerId, SenderId = ownerId,
                    RecipientId = otherId, Status = InvitationStatus.Pending });
            await db.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"/api/journeys/{deletedId}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That(await response.Content.ReadAsStringAsync(), Is.Empty);
        Assert.That((await client.GetAsync($"/api/journeys/{deletedId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await client.GetAsync($"/api/journeys/{retainedOwnerId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await other.GetAsync($"/api/journeys/{retainedOtherId}")).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        using var checkScope = app.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        Assert.That(await checkDb.Journeys.AnyAsync(j => j.Id == deletedId), Is.False);
        Assert.That(await checkDb.JourneyPoints.AnyAsync(p => p.JourneyId == deletedId), Is.False);
        Assert.That(await checkDb.JourneySegments.AnyAsync(s => s.JourneyId == deletedId), Is.False);
        Assert.That(await checkDb.Invitations.AnyAsync(i => i.JourneyId == deletedId), Is.False);
        Assert.That(await checkDb.Users.CountAsync(), Is.EqualTo(2));
        Assert.That(await checkDb.Journeys.CountAsync(), Is.EqualTo(2));
        foreach (var retainedId in new[] { retainedOwnerId, retainedOtherId })
        {
            Assert.That(await checkDb.JourneyPoints.CountAsync(p => p.JourneyId == retainedId), Is.EqualTo(2));
            Assert.That(await checkDb.JourneySegments.CountAsync(s => s.JourneyId == retainedId), Is.EqualTo(1));
        }
        Assert.That(await checkDb.Invitations.CountAsync(i => i.JourneyId == retainedOwnerId), Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeleteJourney_NonOwnerCannotDelete(bool hasSharedAccess)
    {
        var ownerId = await SignupForDelete(client, "River");
        var id = await CreateForDelete(client);
        var before = await client.GetStringAsync($"/api/journeys/{id}");
        using var other = app.CreateApiClient();
        var otherId = await SignupForDelete(other, "Avery");
        if (hasSharedAccess)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            db.Invitations.Add(new Invitation { JourneyId = id, SenderId = ownerId,
                RecipientId = otherId, Status = InvitationStatus.Accepted });
            await db.SaveChangesAsync();
        }

        var response = await other.DeleteAsync($"/api/journeys/{id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await client.GetStringAsync($"/api/journeys/{id}"), Is.EqualTo(before));
        if (hasSharedAccess)
            Assert.That((await other.GetAsync($"/api/journeys/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DeleteJourney_AnonymousUserCannotDelete()
    {
        await SignupForDelete(client, "River");
        var id = await CreateForDelete(client);
        var before = await client.GetStringAsync($"/api/journeys/{id}");
        using var anonymous = app.CreateApiClient();
        var response = await anonymous.DeleteAsync($"/api/journeys/{id}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.Location, Is.Null);
        Assert.That(await client.GetStringAsync($"/api/journeys/{id}"), Is.EqualTo(before));
    }

    [Test]
    public async Task DeleteJourney_MissingJourneyReturns404()
    {
        await SignupForDelete(client, "River");
        var response = await client.DeleteAsync("/api/journeys/unknown-journey");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("message").GetString(), Is.EqualTo("Journey not found."));
    }

    [Test]
    public async Task DuplicateJourney_CreatesIndependentCopy()
    {
        // Arrange: sign up and save the original journey.
        var signup = await client.PostAsJsonAsync("/api/auth/signup",
            new { username = "River", password = "password1" });
        Assert.That(signup.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var created = await client.PostAsJsonAsync("/api/journeys", ValidRequest());
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var original = await created.Content.ReadFromJsonAsync<JsonElement>();
        var originalId = original.GetProperty("id").GetString();

        // Act: duplicate it without supplying a request body.
        var duplicated = await client.PostAsync($"/api/journeys/{originalId}/duplicate", null);
        Assert.That(duplicated.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var copy = await duplicated.Content.ReadFromJsonAsync<JsonElement>();
        var copyId = copy.GetProperty("id").GetString();

        Assert.That(copyId, Is.Not.Null.And.Not.Empty);
        Assert.That(copyId, Is.Not.EqualTo(originalId));
        Assert.That(copy.GetProperty("name").GetString(), Is.EqualTo("Park walk (copy)"));
        foreach (var field in new[] { "owner", "date", "plan", "annotations" })
        {
            Assert.That(copy.GetProperty(field).GetRawText(),
                Is.EqualTo(original.GetProperty(field).GetRawText()),
                $"{field} should be copied.");
        }

        // A separate request verifies the copy was persisted.
        var loadedCopy = await client.GetAsync($"/api/journeys/{copyId}");
        Assert.That(loadedCopy.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var savedCopy = await loadedCopy.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(savedCopy.GetRawText(), Is.EqualTo(copy.GetRawText()));

        // Deleting the copy must leave the original and its route intact.
        var deleted = await client.DeleteAsync($"/api/journeys/{copyId}");
        Assert.That(deleted.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        var loadedOriginal = await client.GetAsync($"/api/journeys/{originalId}");
        Assert.That(loadedOriginal.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var savedOriginal = await loadedOriginal.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(savedOriginal.GetRawText(), Is.EqualTo(original.GetRawText()));
    }

    [Test]
    public async Task DuplicateJourney_DoesNotCopyInvitations()
    {
        var ownerId = await SignupForDelete(client, "River");
        using var recipient = app.CreateApiClient();
        var recipientId = await SignupForDelete(recipient, "Avery");
        var originalId = await CreateForDelete(client);

        // Sharing endpoints are pending; seed accepted access in the test database.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            db.Invitations.Add(new Invitation
            {
                JourneyId = originalId,
                SenderId = ownerId,
                RecipientId = recipientId,
                Status = InvitationStatus.Accepted
            });
            await db.SaveChangesAsync();
        }

        var originalAccess = await recipient.GetAsync($"/api/journeys/{originalId}");
        Assert.That(originalAccess.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var duplicated = await client.PostAsync($"/api/journeys/{originalId}/duplicate", null);
        Assert.That(duplicated.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var copy = await duplicated.Content.ReadFromJsonAsync<JsonElement>();
        var copyId = copy.GetProperty("id").GetString();
        Assert.That(copyId, Is.Not.Null.And.Not.Empty);
        Assert.That(copyId, Is.Not.EqualTo(originalId));

        // Existing sharing grants access to the original only.
        var copyAccess = await recipient.GetAsync($"/api/journeys/{copyId}");
        Assert.That(copyAccess.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var originalAfter = await recipient.GetAsync($"/api/journeys/{originalId}");
        Assert.That(originalAfter.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var checkScope = app.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        Assert.That(await checkDb.Invitations.CountAsync(i => i.JourneyId == originalId), Is.EqualTo(1));
        Assert.That(await checkDb.Invitations.CountAsync(i => i.JourneyId == copyId), Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DuplicateJourney_NonOwnerCannotDuplicate(bool hasSharedAccess)
    {
        var ownerId = await SignupForDelete(client, "River");
        var originalId = await CreateForDelete(client);
        var before = await client.GetStringAsync($"/api/journeys/{originalId}");
        using var other = app.CreateApiClient();
        var otherId = await SignupForDelete(other, "Avery");

        if (hasSharedAccess)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            db.Invitations.Add(new Invitation { JourneyId = originalId,
                SenderId = ownerId, RecipientId = otherId, Status = InvitationStatus.Accepted });
            await db.SaveChangesAsync();
            Assert.That((await other.GetAsync($"/api/journeys/{originalId}")).StatusCode,
                Is.EqualTo(HttpStatusCode.OK));
        }

        var response = await other.PostAsync($"/api/journeys/{originalId}/duplicate", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await client.GetStringAsync($"/api/journeys/{originalId}"), Is.EqualTo(before));

        using var checkScope = app.Services.CreateScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        Assert.That(await checkDb.Journeys.CountAsync(), Is.EqualTo(1));
        Assert.That(await checkDb.JourneyPoints.CountAsync(), Is.EqualTo(2));
        Assert.That(await checkDb.JourneySegments.CountAsync(), Is.EqualTo(1));
        Assert.That(await checkDb.Invitations.CountAsync(), Is.EqualTo(hasSharedAccess ? 1 : 0));
    }

    [Test]
    public async Task DuplicateJourney_AnonymousUserCannotDuplicate()
    {
        await SignupForDelete(client, "River");
        var originalId = await CreateForDelete(client);
        var before = await client.GetStringAsync($"/api/journeys/{originalId}");
        using var anonymous = app.CreateApiClient();

        var response = await anonymous.PostAsync($"/api/journeys/{originalId}/duplicate", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Headers.Location, Is.Null);
        Assert.That(await client.GetStringAsync($"/api/journeys/{originalId}"), Is.EqualTo(before));

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        Assert.That(await db.Journeys.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.JourneyPoints.CountAsync(), Is.EqualTo(2));
        Assert.That(await db.JourneySegments.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task DuplicateJourney_MissingJourneyReturns404()
    {
        await SignupForDelete(client, "River");
        var response = await client.PostAsync("/api/journeys/unknown-journey/duplicate", null);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("message").GetString(), Is.EqualTo("Journey not found."));
        await AssertNoJourneySaved();
    }

    private static async Task<string> SignupForDelete(HttpClient browser, string username)
    {
        var response = await browser.PostAsJsonAsync("/api/auth/signup",
            new { username, password = "password1" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateForDelete(HttpClient browser)
    {
        var response = await browser.PostAsJsonAsync("/api/journeys", ValidRequest());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetString()!;
    }

    private async Task AssertNoJourneySaved()
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        Assert.That(await db.Journeys.CountAsync(), Is.Zero);
        Assert.That(await db.JourneyPoints.CountAsync(), Is.Zero);
        Assert.That(await db.JourneySegments.CountAsync(), Is.Zero);
    }

    private static CreateJourneyRequest ValidRequest() => new(
        "Park walk", "2026-10-11",
        new("walking", "distance", "open", new(
            [
                new("start", new("park", "Park", null, new(-33.86, 151.20))),
                new("end", new("cafe", "Cafe", null, new(-33.87, 151.21)))
            ],
            [new("leg", "start", "end", 100, 90,
                [new(-33.86, 151.20), new(-33.87, 151.21)])],
            100, 90)),
        [new("start", "Meet here", "09:00")]);
}
