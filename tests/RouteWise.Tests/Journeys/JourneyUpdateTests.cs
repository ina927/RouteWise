using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Data;
using RouteWise.Tests.Auth;

namespace RouteWise.Tests.Journeys;

public sealed class JourneyUpdateTests
{
    private SignupApplication app = null!;
    private HttpClient client = null!;
    private string url = null!;
    private JsonElement original;

    [SetUp]
    public async Task SetUp()
    {
        app = new SignupApplication();
        client = app.CreateApiClient();
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>().Database.MigrateAsync();
        var signup = await client.PostAsJsonAsync("/api/auth/signup", new { username = "River", password = "password1" });
        Assert.That(signup.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var request = new CreateJourneyRequest("Park walk", "2026-10-11",
            new("walking", "distance", "open", new(
                [new("start", new("park", "Park", null, new(-33.86, 151.20))),
                 new("end", new("cafe", "Cafe", null, new(-33.87, 151.21)))],
                [new("leg", "start", "end", 100, 90,
                    [new(-33.86, 151.20), new(-33.87, 151.21)])], 100, 90)),
            [new("start", "Meet here", "09:00")]);
        var response = await client.PostAsJsonAsync("/api/journeys", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        original = await response.Content.ReadFromJsonAsync<JsonElement>();
        url = $"/api/journeys/{original.GetProperty("id").GetString()}";
    }

    [TearDown]
    public void TearDown()
    {
        client.Dispose();
        app.Dispose();
    }

    [TestCase("{\"date\":\"2026-10-20\"}", "Park walk", "2026-10-20")]
    [TestCase("{\"date\":\"2028-02-29\"}", "Park walk", "2028-02-29")]
    [TestCase("{\"date\":null}", "Park walk", null)]
    [TestCase("{\"name\":\"  Sunday walk  \"}", "Sunday walk", "2026-10-11")]
    [TestCase("{\"name\":\"Sunday walk\",\"date\":\"2026-10-20\"}", "Sunday walk", "2026-10-20")]
    [TestCase("{}", "Park walk", "2026-10-11")]
    public async Task ValidUpdate_PersistsAndPreservesOtherFields(string json, string name, string? date)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PatchAsync(url, content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var loaded = await client.GetAsync(url);
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await loaded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(saved.GetRawText(), Is.EqualTo(body.GetRawText()));
        Assert.That(saved.GetProperty("name").GetString(), Is.EqualTo(name));
        if (date is null)
            Assert.That(saved.TryGetProperty("date", out _), Is.False);
        else
            Assert.That(saved.GetProperty("date").GetString(), Is.EqualTo(date));
        foreach (var field in new[] { "id", "owner", "plan", "annotations", "access" })
            Assert.That(saved.GetProperty(field).GetRawText(), Is.EqualTo(original.GetProperty(field).GetRawText()));
    }

    [TestCase("\"2026-02-30\"")]
    [TestCase("\"2026-02-29\"")]
    [TestCase("\"20/10/2026\"")]
    [TestCase("\"2026-1-2\"")]
    [TestCase("\"\"")]
    [TestCase("123")]
    [TestCase("true")]
    [TestCase("{}")]
    [TestCase("[]")]
    public async Task InvalidDate_RejectsWholeUpdateIncludingValidName(string dateJson)
    {
        await AssertRejectedUnchanged($"{{\"name\":\"Changed\",\"date\":{dateJson}}}");
    }

    [TestCase("null")]
    [TestCase("\"   \"")]
    [TestCase("123")]
    [TestCase("false")]
    [TestCase("{}")]
    [TestCase("[]")]
    public async Task InvalidName_RejectsWholeUpdateIncludingValidDate(string nameJson)
    {
        await AssertRejectedUnchanged($"{{\"name\":{nameJson},\"date\":\"2026-10-20\"}}");
    }

    [TestCase("[]", "[]")]
    [TestCase("[{\"pointId\":\"end\",\"note\":\"Meet Alex\",\"plannedTime\":\"12:30\"}]",
        "[{\"pointId\":\"end\",\"note\":\"Meet Alex\",\"plannedTime\":\"12:30\"}]")]
    [TestCase("[{\"pointId\":\"start\",\"note\":\"New note\"}]", "[{\"pointId\":\"start\",\"note\":\"New note\"}]")]
    [TestCase("[{\"pointId\":\"start\",\"plannedTime\":\"00:00\"}]", "[{\"pointId\":\"start\",\"plannedTime\":\"00:00\"}]")]
    [TestCase("[{\"pointId\":\"start\"}]", "[]")]
    public async Task AnnotationReplacement_PersistsAndPreservesRoute(string annotations, string expected)
    {
        using var content = new StringContent($"{{\"annotations\":{annotations}}}", Encoding.UTF8, "application/json");
        var response = await client.PatchAsync(url, content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var loaded = await client.GetAsync(url);
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await loaded.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(saved.GetRawText(), Is.EqualTo(body.GetRawText()));
        Assert.That(saved.GetProperty("annotations").GetRawText(), Is.EqualTo(expected));
        foreach (var field in new[] { "id", "owner", "name", "date", "plan", "access" })
            Assert.That(saved.GetProperty(field).GetRawText(), Is.EqualTo(original.GetProperty(field).GetRawText()));
    }

    [TestCase("null")]
    [TestCase("{}")]
    [TestCase("123")]
    [TestCase("[null]")]
    [TestCase("[{}]")]
    [TestCase("[{\"pointId\":\"missing\"}]")]
    [TestCase("[{\"pointId\":\"start\"},{\"pointId\":\"start\"}]")]
    [TestCase("[{\"pointId\":\"start\",\"plannedTime\":\"24:00\"}]")]
    [TestCase("[{\"pointId\":\"start\",\"plannedTime\":\"9:00\"}]")]
    [TestCase("[{\"pointId\":\"start\",\"note\":123}]")]
    public async Task InvalidAnnotations_RejectWholeUpdate(string annotations)
    {
        await AssertRejectedUnchanged($"{{\"name\":\"Changed\",\"date\":null,\"annotations\":{annotations}}}");
    }

    private static JourneyPlanDto ReplacementPlan() => new("driving", "time", "fixed", new(
        [new("start", new("park", "Park", null, new(-33.86, 151.20))),
         new("gallery", new("gallery-place", "Gallery", null, new(-33.88, 151.22)))],
        [new("leg", "start", "gallery", 250, 120,
            [new(-33.86, 151.20), new(-33.88, 151.22)])], 250, 120));

    [Test]
    public async Task PlanReplacement_PreservesIdentitySharingAndSurvivingAnnotations()
    {
        // Seed a note on the stop that will be removed as well as the surviving stop.
        var notes = await client.PatchAsJsonAsync(url, new { annotations = new[]
        {
            new { pointId = "start", note = "Keep", plannedTime = "09:00" },
            new { pointId = "end", note = "Remove", plannedTime = "10:00" }
        } });
        Assert.That(notes.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var guest = app.CreateApiClient();
        var signup = await guest.PostAsJsonAsync("/api/auth/signup", new { username = "Avery", password = "password1" });
        Assert.That(signup.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var guestId = (await signup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        var invite = await client.PostAsJsonAsync($"{url}/invitations", new { recipientId = guestId });
        Assert.That(invite.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var inviteId = (await invite.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        Assert.That((await guest.PatchAsJsonAsync($"/api/invitations/{inviteId}", new { status = "accepted" })).StatusCode,
            Is.EqualTo(HttpStatusCode.OK));

        var plan = ReplacementPlan();
        var response = await client.PatchAsJsonAsync(url, new { plan });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await ReadJourney();
        Assert.That(saved.GetProperty("plan").GetRawText(),
            Is.EqualTo(JsonSerializer.Serialize(plan, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        foreach (var field in new[] { "id", "owner", "name", "date" })
            Assert.That(saved.GetProperty(field).GetRawText(), Is.EqualTo(original.GetProperty(field).GetRawText()));
        var annotations = saved.GetProperty("annotations");
        Assert.That(annotations.GetArrayLength(), Is.EqualTo(1));
        Assert.That(annotations[0].GetProperty("pointId").GetString(), Is.EqualTo("start"));
        Assert.That(annotations[0].GetProperty("note").GetString(), Is.EqualTo("Keep"));
        Assert.That(annotations[0].GetProperty("plannedTime").GetString(), Is.EqualTo("09:00"));
        var shared = await guest.GetAsync(url);
        Assert.That(shared.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await shared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("plan").GetRawText(),
            Is.EqualTo(saved.GetProperty("plan").GetRawText()));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
        Assert.That(await db.Journeys.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.JourneyPoints.CountAsync(), Is.EqualTo(2));
        Assert.That(await db.JourneySegments.CountAsync(), Is.EqualTo(1));
        Assert.That((await db.Invitations.SingleAsync()).Id, Is.EqualTo(inviteId));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task PlanReplacement_AppliesExplicitAnnotationsAndMetadata(bool clear)
    {
        var annotations = clear ? Array.Empty<PointAnnotationDto>()
            : new[] { new PointAnnotationDto("gallery", "New stop", "12:30") };
        var response = await client.PatchAsJsonAsync(url,
            new { name = "Updated", date = (string?)null, plan = ReplacementPlan(), annotations });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var saved = await ReadJourney();
        Assert.That(saved.GetProperty("name").GetString(), Is.EqualTo("Updated"));
        Assert.That(saved.TryGetProperty("date", out _), Is.False);
        Assert.That(saved.GetProperty("annotations").GetRawText(),
            Is.EqualTo(JsonSerializer.Serialize(annotations, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    }

    [TestCase("null")]
    [TestCase("[]")]
    [TestCase("123")]
    [TestCase("{}")]
    [TestCase("{\"travelMode\":123}")]
    public async Task InvalidPlan_PreservesOriginal(string plan)
    {
        await AssertRejectedUnchanged($"{{\"name\":\"Changed\",\"plan\":{plan}}}");
    }

    [Test]
    public async Task PlanReplacement_RejectsExplicitAnnotationForRemovedPoint()
    {
        var json = JsonSerializer.Serialize(new { plan = ReplacementPlan(),
            annotations = new[] { new PointAnnotationDto("end", "Removed stop", null) } },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await AssertRejectedUnchanged(json);
    }

    [Test]
    public async Task PlanReplacement_DatabaseFailureRollsBackDeletedRouteAndMetadata()
    {
        // A test-only SQLite trigger fails insertion after old route deletions.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RouteWiseDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER fail_replacement BEFORE INSERT ON JourneyPoints
                WHEN NEW.Name = 'Gallery'
                BEGIN SELECT RAISE(ABORT, 'test replacement failure'); END;
                """);
        }
        var response = await client.PatchAsJsonAsync(url,
            new { name = "Changed", date = (string?)null, plan = ReplacementPlan() });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
        Assert.That((await ReadJourney()).GetRawText(), Is.EqualTo(original.GetRawText()));
    }

    private async Task<JsonElement> ReadJourney()
    {
        var loaded = await client.GetAsync(url);
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await loaded.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task AssertRejectedUnchanged(string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PatchAsync(url, content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(error.GetProperty("message").GetString(), Is.Not.Null.And.Not.Empty);
        var loaded = await client.GetAsync(url);
        Assert.That(loaded.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await loaded.Content.ReadFromJsonAsync<JsonElement>()).GetRawText(), Is.EqualTo(original.GetRawText()));
    }
}
