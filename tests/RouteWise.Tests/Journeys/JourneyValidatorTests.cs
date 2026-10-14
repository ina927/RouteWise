using NUnit.Framework;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Validation;

namespace RouteWise.Tests.Journeys;

public sealed class JourneyValidatorTests
{
    [TestCase(100, true)]
    [TestCase(101, false)]
    public void NameLength_IsCheckedAfterTrimming(int length, bool valid) =>
        Assert.That(JourneyValidator.Validate(Valid() with { Name = $"  {new string('a', length)}  " }) is null,
            Is.EqualTo(valid));

    [TestCase(2000, true)]
    [TestCase(2001, false)]
    public void NoteLength_IsCheckedAfterTrimming(int length, bool valid) =>
        Assert.That(JourneyValidator.Validate(Valid() with { Annotations = [new("p0", $"  {new string('a', length)}  ", null)] }) is null,
            Is.EqualTo(valid));

    [TestCase("open", 2)]
    [TestCase("fixed", 2)]
    [TestCase("returnToStart", 2)]
    [TestCase("open", 10)]
    [TestCase("returnToStart", 10)]
    public void ValidRoutes_AreAccepted(string endMode, int count)
    {
        var request = Valid(count);
        request = request with { Plan = request.Plan! with { EndMode = endMode } };
        if (endMode == "returnToStart")
            request.Plan.Route.Points[^1] = request.Plan.Route.Points[^1] with { Place = request.Plan.Route.Points[0].Place };
        Assert.That(JourneyValidator.Validate(request), Is.Null);
    }

    [Test]
    public void RepeatedPlace_WithSeparateOccurrenceNotesAndUnorderedTimes_IsAccepted()
    {
        var request = Valid();
        request.Plan!.Route.Points[1] = request.Plan.Route.Points[1] with { Place = request.Plan.Route.Points[0].Place };
        request = request with
        {
            Plan = request.Plan with { EndMode = "returnToStart" },
            Annotations = [new("p0", "Start", "18:00"), new("p1", "Return", "09:00")]
        };
        Assert.That(JourneyValidator.Validate(request), Is.Null);
    }

    [TestCase(null, true)]
    [TestCase("2028-02-29", true)]
    [TestCase("2026-02-29", false)]
    [TestCase("2026-10-10", true)]
    [TestCase("10/10/2026", false)]
    [TestCase("", false)]
    public void DateMustBeAnExactCalendarDate(string? date, bool valid) =>
        Assert.That(JourneyValidator.Validate(Valid() with { Date = date }) is null, Is.EqualTo(valid));

    [TestCase("00:00", true)]
    [TestCase("23:59", true)]
    [TestCase(null, true)]
    [TestCase("24:00", false)]
    [TestCase("12:60", false)]
    [TestCase("9:00", false)]
    [TestCase("09:00:00", false)]
    [TestCase("", false)]
    public void PlannedTimeMustBeExact(string? time, bool valid) =>
        Assert.That(JourneyValidator.Validate(Valid() with { Annotations = [new("p0", null, time)] }) is null,
            Is.EqualTo(valid));

    [TestCase(-1)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void InvalidMetrics_AreRejected(double value)
    {
        var request = Valid();
        var route = request.Plan!.Route;
        var segment = route.Segments[0];
        route.Segments[0] = segment with { DistanceMeters = value };
        Assert.That(JourneyValidator.Validate(request), Is.Not.Null);
        route.Segments[0] = segment with { DurationSeconds = value };
        Assert.That(JourneyValidator.Validate(request), Is.Not.Null);
        route.Segments[0] = segment;
        Assert.That(JourneyValidator.Validate(request with { Plan = request.Plan with { Route = route with { TotalDistanceMeters = value } } }), Is.Not.Null);
        Assert.That(JourneyValidator.Validate(request with { Plan = request.Plan with { Route = route with { TotalDurationSeconds = value } } }), Is.Not.Null);
    }

    [Test]
    public void TotalsMustMatch_AndOverflowIsRejected()
    {
        var request = Valid(3);
        var route = request.Plan!.Route;
        Assert.That(JourneyValidator.Validate(request with { Plan = request.Plan with { Route = route with { TotalDistanceMeters = 999 } } }), Is.Not.Null);
        Assert.That(JourneyValidator.Validate(request with { Plan = request.Plan with { Route = route with { TotalDurationSeconds = 999 } } }), Is.Not.Null);
        route.Segments[0] = route.Segments[0] with { DistanceMeters = double.MaxValue };
        route.Segments[1] = route.Segments[1] with { DistanceMeters = double.MaxValue };
        Assert.That(JourneyValidator.Validate(request), Is.Not.Null);
    }

    [Test]
    public void ZeroCosts_AndFloatingPointRounding_AreAccepted()
    {
        var request = Valid(3);
        var route = request.Plan!.Route;
        route.Segments[0] = route.Segments[0] with { DistanceMeters = 0.1, DurationSeconds = 0 };
        route.Segments[1] = route.Segments[1] with { DistanceMeters = 0.2, DurationSeconds = 0 };
        request = request with { Plan = request.Plan with { Route = route with { TotalDistanceMeters = 0.3, TotalDurationSeconds = 0 } } };
        Assert.That(JourneyValidator.Validate(request), Is.Null);
    }

    [TestCase(91, 0)]
    [TestCase(0, -181)]
    [TestCase(double.NaN, 0)]
    [TestCase(0, double.PositiveInfinity)]
    public void InvalidCoordinates_AreRejectedForPlacesAndPaths(double lat, double lng)
    {
        var request = Valid();
        var route = request.Plan!.Route;
        var original = route.Points[0];
        route.Points[0] = original with { Place = original.Place with { Coordinates = new(lat, lng) } };
        Assert.That(JourneyValidator.Validate(request), Is.Not.Null);
        route.Points[0] = original;
        route.Segments[0].Path[0] = new(lat, lng);
        Assert.That(JourneyValidator.Validate(request), Is.Not.Null);
    }

    [Test]
    public void SnappedPathsNeedNotExactlyMatchPlaceCoordinates()
    {
        var request = Valid();
        request.Plan!.Route.Segments[0].Path[0] = new(-90, -180);
        request.Plan.Route.Segments[0].Path[1] = new(90, 180);
        Assert.That(JourneyValidator.Validate(request), Is.Null);
    }

    [TestCaseSource(nameof(InvalidRequests))]
    public void InvalidStructure_ReturnsAnErrorWithoutThrowing(CreateJourneyRequest? request)
    {
        Assert.That(JourneyValidator.Validate(request), Is.Not.Null.And.Not.Empty);
    }

    private static IEnumerable<TestCaseData> InvalidRequests()
    {
        yield return new TestCaseData(new object?[] { null }).SetName("Null request");
        yield return Case("Blank name", r => r with { Name = " " });
        yield return Case("Missing plan", r => r with { Plan = null });
        yield return Case("Unsupported mode", r => r with { Plan = r.Plan! with { TravelMode = "flying" } });
        yield return Case("Unsupported objective", r => r with { Plan = r.Plan! with { Objective = "cost" } });
        yield return Case("Unsupported end", r => r with { Plan = r.Plan! with { EndMode = "unknown" } });
        yield return Case("Missing route", r => r with { Plan = r.Plan! with { Route = null! } });
        yield return Case("Missing points", r => r with { Plan = r.Plan! with { Route = r.Plan.Route with { Points = null! } } });
        yield return new TestCaseData(Valid(1)).SetName("Too few points");
        yield return new TestCaseData(Valid(11)).SetName("Too many points");
        yield return Case("Null point", r => { r.Plan!.Route.Points[0] = null!; return r; });
        yield return Case("Blank point ID", r => { r.Plan!.Route.Points[0] = r.Plan.Route.Points[0] with { Id = " " }; return r; });
        yield return Case("Duplicate point ID", r => { r.Plan!.Route.Points[1] = r.Plan.Route.Points[1] with { Id = "p0" }; return r; });
        yield return Case("Missing place", r => { r.Plan!.Route.Points[0] = r.Plan.Route.Points[0] with { Place = null! }; return r; });
        yield return Case("Blank place ID", r => { var p = r.Plan!.Route.Points[0]; r.Plan.Route.Points[0] = p with { Place = p.Place with { PlaceId = "" } }; return r; });
        yield return Case("Blank place name", r => { var p = r.Plan!.Route.Points[0]; r.Plan.Route.Points[0] = p with { Place = p.Place with { Name = "" } }; return r; });
        yield return Case("Missing coordinates", r => { var p = r.Plan!.Route.Points[0]; r.Plan.Route.Points[0] = p with { Place = p.Place with { Coordinates = null! } }; return r; });
        yield return Case("Return to different place", r => r with { Plan = r.Plan! with { EndMode = "returnToStart" } });
        yield return Case("Missing segments", r => r with { Plan = r.Plan! with { Route = r.Plan.Route with { Segments = null! } } });
        yield return Case("Wrong segment count", r => { r.Plan!.Route.Segments.Clear(); return r; });
        yield return Case("Null segment", r => { r.Plan!.Route.Segments[0] = null!; return r; });
        yield return Case("Blank segment ID", r => { r.Plan!.Route.Segments[0] = r.Plan.Route.Segments[0] with { Id = "" }; return r; });
        var duplicate = Valid(3);
        duplicate.Plan!.Route.Segments[1] = duplicate.Plan.Route.Segments[1] with { Id = "s0" };
        yield return new TestCaseData(duplicate).SetName("Duplicate segment ID");
        yield return Case("Wrong segment endpoints", r => { r.Plan!.Route.Segments[0] = r.Plan.Route.Segments[0] with { FromPointId = "p1" }; return r; });
        yield return Case("Missing path", r => { r.Plan!.Route.Segments[0] = r.Plan.Route.Segments[0] with { Path = null! }; return r; });
        yield return Case("Short path", r => { r.Plan!.Route.Segments[0].Path.RemoveAt(0); return r; });
        yield return Case("Null path coordinate", r => { r.Plan!.Route.Segments[0].Path[0] = null!; return r; });
        yield return Case("Null annotation", r => r with { Annotations = [null!] });
        yield return Case("Unknown annotation point", r => r with { Annotations = [new("missing", null, null)] });
        yield return Case("Duplicate annotations", r => r with { Annotations = [new("p0", null, null), new("p0", "Note", null)] });
    }

    private static TestCaseData Case(string name, Func<CreateJourneyRequest, CreateJourneyRequest> change) =>
        new TestCaseData(change(Valid())).SetName(name);

    private static CreateJourneyRequest Valid(int count = 2)
    {
        var points = Enumerable.Range(0, count).Select(i =>
            new RoutePointDto($"p{i}", new PlaceDto($"place{i}", "Place", null, new(-33.8, 151.2)))).ToList();
        var segments = Enumerable.Range(0, count - 1).Select(i =>
            new RouteSegmentDto($"s{i}", $"p{i}", $"p{i + 1}", 100, 60, [new(-33.8, 151.2), new(-33.81, 151.21)])).ToList();
        return new("Walk", null, new("walking", "distance", "open", new(points, segments, 100 * (count - 1), 60 * (count - 1))), null);
    }
}
