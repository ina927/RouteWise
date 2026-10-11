using NUnit.Framework;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Mapping;
using RouteWise.Api.Models;
using RouteWise.Api.Validation;

namespace RouteWise.Tests.Journeys;

public sealed class JourneyMapperTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void ToEntity_PreservesOccurrenceMetadataAndCreatesIndependentGraphs(bool includeMetadata)
    {
        var place = new PlaceDto("park", "Park", "Park Road", new(-33.86, 151.2));
        var request = new CreateJourneyRequest("  Walk  ", includeMetadata ? "2026-10-10" : null,
            new("walking", "distance", "returnToStart",
                new([new("start", place), new("return", place)],
                    [new("leg", "start", "return", 100, 90, [new(-33.86, 151.2), new(-33.87, 151.21)])], 100, 90)),
            includeMetadata ? [new("return", "Finish", "15:30"), new("start", "Begin", "09:00")] : null);
        Assert.That(JourneyValidator.Validate(request), Is.Null);

        var journey = JourneyMapper.ToEntity(request, "owner-id");
        var second = JourneyMapper.ToEntity(request, "owner-id");
        Assert.Multiple(() =>
        {
            Assert.That(journey.Id, Is.Not.Empty.And.Not.EqualTo(second.Id));
            Assert.That(journey.OwnerId, Is.EqualTo("owner-id"));
            Assert.That(journey.Name, Is.EqualTo("Walk"));
            Assert.That(journey.Date, Is.EqualTo(includeMetadata ? new DateOnly(2026, 10, 10) : (DateOnly?)null));
            Assert.That(journey.TravelMode, Is.EqualTo(TravelMode.Walking));
            Assert.That(journey.Objective, Is.EqualTo(OptimisationObjective.Distance));
            Assert.That(journey.EndMode, Is.EqualTo(EndMode.ReturnToStart));
            Assert.That(journey.Points.Select(p => p.Id), Is.EqualTo(new[] { "start", "return" }));
            Assert.That(journey.Points.Select(p => p.Position), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(journey.Points.All(p => p.JourneyId == journey.Id), Is.True);
            Assert.That(journey.Points[0].Address, Is.EqualTo("Park Road"));
            Assert.That(journey.Points[0].Latitude, Is.EqualTo(-33.86));
            Assert.That(journey.Points[0].Note, Is.EqualTo(includeMetadata ? "Begin" : null));
            Assert.That(journey.Points[1].Note, Is.EqualTo(includeMetadata ? "Finish" : null));
            Assert.That(journey.Points[1].PlannedTime, Is.EqualTo(includeMetadata ? new TimeOnly(15, 30) : (TimeOnly?)null));
            Assert.That(journey.Segments[0].JourneyId, Is.EqualTo(journey.Id));
            Assert.That(journey.Segments[0].FromPointId, Is.EqualTo("start"));
            Assert.That(journey.Segments[0].ToPointId, Is.EqualTo("return"));
            Assert.That(journey.Segments[0].DistanceMeters, Is.EqualTo(100));
            Assert.That(journey.Segments[0].DurationSeconds, Is.EqualTo(90));
            Assert.That(journey.Segments[0].Path[1].Lng, Is.EqualTo(151.21));
        });

        journey.Segments[0].Path[0].Lat = 0;
        journey.Points[0].Note = "Changed";
        Assert.That(second.Segments[0].Path[0].Lat, Is.EqualTo(-33.86));
        Assert.That(request.Plan!.Route.Segments[0].Path[0].Lat, Is.EqualTo(-33.86));
        Assert.That(second.Points[0].Note, Is.EqualTo(includeMetadata ? "Begin" : null));
    }
}
