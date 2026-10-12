using System.Text.Json;
using NUnit.Framework;
using RouteWise.Api.Mapping;
using RouteWise.Api.Models;

namespace RouteWise.Tests.Journeys;

public sealed class JourneyResponseTests
{
    [TestCase(TravelMode.Walking, OptimisationObjective.Distance, EndMode.ReturnToStart, "walking", "distance", "returnToStart", "owner")]
    [TestCase(TravelMode.Driving, OptimisationObjective.Time, EndMode.Fixed, "driving", "time", "fixed", "shared")]
    [TestCase(TravelMode.Walking, OptimisationObjective.Time, EndMode.Open, "walking", "time", "open", "owner")]
    public void Response_RestoresOrderAndUsesContractValues(TravelMode mode, OptimisationObjective objective,
        EndMode endMode, string expectedMode, string expectedObjective, string expectedEndMode, string access)
    {
        var journey = CreateJourney();
        journey.TravelMode = mode;
        journey.Objective = objective;
        journey.EndMode = endMode;
        var response = JourneyMapper.ToResponse(journey, access);
        Assert.Multiple(() =>
        {
            Assert.That(response.Plan.Route.Points.Select(p => p.Id), Is.EqualTo(new[] { "start", "middle", "end" }));
            Assert.That(response.Plan.Route.Segments.Select(s => s.Id), Is.EqualTo(new[] { "leg1", "leg2" }));
            Assert.That(response.Plan.Route.TotalDistanceMeters, Is.EqualTo(300));
            Assert.That(response.Plan.Route.TotalDurationSeconds, Is.EqualTo(150));
            Assert.That(response.Plan.Route.Segments[0].Path[1].Lng, Is.EqualTo(151.21));
            Assert.That(response.Date, Is.EqualTo("2026-10-10"));
            Assert.That(response.Plan.TravelMode, Is.EqualTo(expectedMode));
            Assert.That(response.Plan.Objective, Is.EqualTo(expectedObjective));
            Assert.That(response.Plan.EndMode, Is.EqualTo(expectedEndMode));
            Assert.That(response.Access, Is.EqualTo(access));
            Assert.That(response.Annotations.Select(a => a.PointId), Is.EqualTo(new[] { "start", "end" }));
            Assert.That(response.Annotations[0].Note, Is.EqualTo(""));
            Assert.That(response.Annotations[1].PlannedTime, Is.EqualTo("09:05"));
            Assert.That(journey.Points[0].Id, Is.EqualTo("end"), "Mapping must not reorder the entity collections.");
        });
        journey.Segments.Single(s => s.Id == "leg1").Path[0].Lat = 0;
        Assert.That(response.Plan.Route.Segments[0].Path[0].Lat, Is.EqualTo(-33.86));
    }

    [Test]
    public void Json_OmitsAbsentFieldsAndDoesNotExposeStorageOrCredentials()
    {
        var journey = CreateJourney();
        journey.Date = null;
        var json = JsonSerializer.SerializeToElement(JourneyMapper.ToResponse(journey, "owner"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Multiple(() =>
        {
            Assert.That(json.TryGetProperty("date", out _), Is.False);
            Assert.That(json.GetProperty("owner").EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "username" }));
            var point = json.GetProperty("plan").GetProperty("route").GetProperty("points")[0];
            Assert.That(point.EnumerateObject().Select(p => p.Name), Is.EquivalentTo(new[] { "id", "place" }));
            Assert.That(point.GetProperty("place").TryGetProperty("address", out _), Is.False);
            var annotations = json.GetProperty("annotations");
            Assert.That(annotations[0].TryGetProperty("plannedTime", out _), Is.False);
            Assert.That(annotations[0].GetProperty("note").GetString(), Is.EqualTo(""));
            Assert.That(annotations[1].TryGetProperty("note", out _), Is.False);
        });
        foreach (var point in journey.Points) { point.Note = null; point.PlannedTime = null; }
        Assert.That(JourneyMapper.ToResponse(journey, "shared").Annotations, Is.Empty);
    }

    private static Journey CreateJourney() => new()
    {
        Name = "Walk", Date = new DateOnly(2026, 10, 10),
        Owner = new User { Id = "owner", Username = "River", PasswordHash = "never-expose" },
        Points =
        [
            new() { Id = "end", Position = 2, PlaceId = "park", Name = "Park", PlannedTime = new TimeOnly(9, 5) },
            new() { Id = "middle", Position = 1, PlaceId = "museum", Name = "Museum" },
            new() { Id = "start", Position = 0, PlaceId = "park", Name = "Park", Note = "" }
        ],
        Segments =
        [
            new() { Id = "leg2", Position = 1, FromPointId = "middle", ToPointId = "end", DistanceMeters = 200, DurationSeconds = 90 },
            new() { Id = "leg1", Position = 0, FromPointId = "start", ToPointId = "middle", DistanceMeters = 100, DurationSeconds = 60,
                Path = [new() { Lat = -33.86, Lng = 151.2 }, new() { Lat = -33.87, Lng = 151.21 }] }
        ]
    };
}
