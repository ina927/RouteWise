
using NUnit.Framework;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Contracts.Routes;
using RouteWise.Api.Services;

namespace RouteWise.Tests.Services;

[TestFixture]
public class RoutePlanningServiceTests
{
    private FakeRoutingService _routing = null!;
    private FakeOptimiser _optimiser = null!;
    private RoutePlanningService _service = null!;

    [SetUp]
    public void SetUp()
    {
        // create fake dependencies
        _routing = new FakeRoutingService();
        _optimiser = new FakeOptimiser();

        // create service being tested
        _service = new RoutePlanningService(_routing, _optimiser);
    }

    private static RoutePointDto Point(int number)
    {
        return new RoutePointDto(
            $"point-{number}",
            new PlaceDto(
                $"place-{number}",
                $"Location {number}",
                null,
                new CoordinatesDto(-33.8 + number * 0.01, 151.2)));
    }

    private static OptimizeRouteRequest Request(
        string endMode = "open",
        int destinationCount = 2,
        string objective = "distance",
        string travelMode = "driving")
    {
        return new OptimizeRouteRequest(
            Start: Point(0),
            Destinations: Enumerable.Range(1, destinationCount)
                .Select(Point)
                .ToList(),
            TravelMode: travelMode,
            Objective: objective,
            EndMode: endMode,
            End: endMode == "fixed" ? Point(destinationCount + 1) : null,
            ReturnPointId: endMode == "returnToStart"
                ? "return-visit"
                : null);
    }

    [Test]
    public async Task OpenRoute_UsesUnfixedEnd()
    {
        // optimise an open route
        await _service.OptimiseAsync(Request());

        Assert.Multiple(() =>
        {
            Assert.That(_optimiser.Called, Is.True);
            Assert.That(_optimiser.FixedEnd, Is.False);
            Assert.That(_optimiser.ReturnToStart, Is.False);
        });
    }

    [Test]
    public async Task FixedRoute_KeepsEndFixed()
    {
        // optimise a route with a fixed end
        var result = await _service.OptimiseAsync(Request("fixed"));

        Assert.Multiple(() =>
        {
            Assert.That(_optimiser.FixedEnd, Is.True);
            Assert.That(_optimiser.ReturnToStart, Is.False);
            Assert.That(result.Points[^1].Id, Is.EqualTo("point-3"));
        });
    }

    [Test]
    public async Task ReturnToStart_UsesReturnPointId()
    {
        // optimise a return-to-start route
        var result = await _service.OptimiseAsync(Request("returnToStart"));

        Assert.Multiple(() =>
        {
            Assert.That(_optimiser.ReturnToStart, Is.True);
            Assert.That(result.Points.Count, Is.EqualTo(4));
            Assert.That(result.Points[^1].Id, Is.EqualTo("return-visit"));
            Assert.That(result.Points[^1].Place.PlaceId,
                Is.EqualTo(result.Points[0].Place.PlaceId));
        });
    }

    [Test]
    public async Task ReturnToStart_TenLocationsProducesElevenPoints()
    {
        // use 10 entered locations
        var result = await _service.OptimiseAsync(
            Request("returnToStart", destinationCount: 9));

        Assert.Multiple(() =>
        {
            Assert.That(_routing.MatrixPointCount, Is.EqualTo(10));
            Assert.That(result.Points.Count, Is.EqualTo(11));
        });
    }

    [TestCase("distance")]
    [TestCase("time")]
    public async Task Objective_IsPassedToRoutingService(string objective)
    {
        // check cost matrix objective
        await _service.OptimiseAsync(Request(objective: objective));

        Assert.That(_routing.Objective, Is.EqualTo(objective));
    }

    [TestCase("walking")]
    [TestCase("driving")]
    public async Task TravelMode_IsPassedToRoutingService(string travelMode)
    {
        // check travel mode
        await _service.OptimiseAsync(Request(travelMode: travelMode));

        Assert.Multiple(() =>
        {
            Assert.That(_routing.MatrixTravelMode, Is.EqualTo(travelMode));
            Assert.That(_routing.RouteTravelMode, Is.EqualTo(travelMode));
        });
    }

    [Test]
    public async Task CalculateRoute_PreservesEnteredOrder()
    {
        // calculate without optimisation
        var points = new List<RoutePointDto>
        {
            Point(2),
            Point(0),
            Point(1)
        };

        var result = await _service.CalculateAsync(
            new CalculateRouteRequest(points, "walking"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Points.Select(p => p.Id),
                Is.EqualTo(new[] { "point-2", "point-0", "point-1" }));
            Assert.That(_optimiser.Called, Is.False);
            Assert.That(_routing.MatrixPointCount, Is.EqualTo(0));
        });
    }

    [TestCase("cycling", "distance", "open")]
    [TestCase("driving", "price", "open")]
    [TestCase("driving", "distance", "unknown")]
    public void InvalidSettings_ThrowsArgumentException(
        string travelMode,
        string objective,
        string endMode)
    {
        // reject unsupported settings
        var request = Request(
            endMode: endMode,
            objective: objective,
            travelMode: travelMode);

        Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.OptimiseAsync(request));
    }

    [Test]
    public void MissingFixedEnd_ThrowsArgumentException()
    {
        // reject fixed route without end
        var request = Request("fixed") with { End = null };

        Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.OptimiseAsync(request));
    }

    [Test]
    public void DuplicateReturnId_ThrowsArgumentException()
    {
        // reject reused occurrence ID
        var request = Request("returnToStart") with
        {
            ReturnPointId = "point-0"
        };

        Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.OptimiseAsync(request));
    }

    [Test]
    public void TooManyLocations_ThrowsArgumentException()
    {
        // reject more than 10 entered locations
        var request = Request(destinationCount: 10);

        Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.OptimiseAsync(request));
    }


    [Test]
    public void Calculate_InvalidTravelMode_ThrowsArgumentException()
    {
        // reject unsupported travel mode
        var request = new CalculateRouteRequest(
            new List<RoutePointDto> { Point(0), Point(1) },
            "cycling");

        Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CalculateAsync(request));
    }

    [Test]
    public void Calculate_TooFewPoints_ThrowsArgumentException()
    {
        // reject routes with fewer than 2 points
        var request = new CalculateRouteRequest(
            new List<RoutePointDto> { Point(0) },
            "driving");

        Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CalculateAsync(request));
    }

    [Test]
    public void Calculate_TooManyPoints_ThrowsArgumentException()
    {
        // reject routes with more than 11 occurrences
        var points = Enumerable.Range(0, 12)
            .Select(Point)
            .ToList();

        var request = new CalculateRouteRequest(points, "driving");

        Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CalculateAsync(request));
    }

    [Test]
    public void Calculate_DuplicatePointIds_ThrowsArgumentException()
    {
        // reject duplicate occurrence IDs
        var points = new List<RoutePointDto>
    {
        Point(0),
        Point(1) with { Id = "point-0" }
    };

        var request = new CalculateRouteRequest(points, "driving");

        Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CalculateAsync(request));
    }

    [TestCase(100, 151.2)]
    [TestCase(-33.8, 200)]
    [TestCase(double.NaN, 151.2)]
    public void Calculate_InvalidCoordinates_ThrowsArgumentException(
        double lat, double lng)
    {
        // reject invalid coordinates
        var invalidPoint = Point(1) with
        {
            Place = Point(1).Place with
            {
                Coordinates = new CoordinatesDto(lat, lng)
            }
        };

        var request = new CalculateRouteRequest(
            new List<RoutePointDto> { Point(0), invalidPoint },
            "driving");

        Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CalculateAsync(request));
    }

    [Test]
    public void Calculate_MissingPlaceId_ThrowsArgumentException()
    {
        // reject missing Google place ID
        var invalidPoint = Point(1) with
        {
            Place = Point(1).Place with { PlaceId = "" }
        };

        var request = new CalculateRouteRequest(
            new List<RoutePointDto> { Point(0), invalidPoint },
            "driving");

        Assert.ThrowsAsync<ArgumentException>(async () => await _service.CalculateAsync(request));
    }

    [Test]
    public async Task Calculate_ElevenPoints_Succeeds()
    {
        // allow 10 locations plus a return visit
        var points = Enumerable.Range(0, 10)
            .Select(Point)
            .ToList();

        // add return occurrence with a unique ID
        points.Add(new RoutePointDto(
            "return-visit",
            points[0].Place));

        var request = new CalculateRouteRequest(points, "driving");

        var result = await _service.CalculateAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Points.Count, Is.EqualTo(11));
            Assert.That(result.Segments.Count, Is.EqualTo(10));
            Assert.That(result.Points[^1].Id, Is.EqualTo("return-visit"));
            Assert.That(_optimiser.Called, Is.False);
        });
    }

    // fake Google Routes implementation
    private sealed class FakeRoutingService : IRoutingService
    {
        public int MatrixPointCount { get; private set; }
        public string? Objective { get; private set; }
        public string? MatrixTravelMode { get; private set; }
        public string? RouteTravelMode { get; private set; }

        public Task<double[,]> GetCostMatrixAsync(
            IReadOnlyList<RoutePointDto> points,
            string travelMode,
            string objective,
            CancellationToken cancellationToken = default)
        {
            MatrixPointCount = points.Count;
            MatrixTravelMode = travelMode;
            Objective = objective;

            // create a simple symmetric cost matrix
            var matrix = new double[points.Count, points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = 0; j < points.Count; j++)
                {
                    matrix[i, j] = i == j ? 0 : 1;
                }
            }

            return Task.FromResult(matrix);
        }

        public Task<RouteResultDto> CalculateRouteAsync(
            IReadOnlyList<RoutePointDto> points,
            string travelMode,
            CancellationToken cancellationToken = default)
        {
            RouteTravelMode = travelMode;

            // create fake segments between consecutive points
            var segments = new List<RouteSegmentDto>();

            for (int i = 0; i < points.Count - 1; i++)
            {
                segments.Add(new RouteSegmentDto(
                    $"segment-{i}",
                    points[i].Id,
                    points[i + 1].Id,
                    100,
                    60,
                    new List<CoordinatesDto>
                    {
                        points[i].Place.Coordinates,
                        points[i + 1].Place.Coordinates
                    }));
            }

            return Task.FromResult(new RouteResultDto(
                points.ToList(),
                segments,
                segments.Sum(s => s.DistanceMeters),
                segments.Sum(s => s.DurationSeconds)));
        }
    }

    // fake optimiser that preserves the entered order
    private sealed class FakeOptimiser : IRouteOptimiser
    {
        public bool Called { get; private set; }
        public bool FixedEnd { get; private set; }
        public bool ReturnToStart { get; private set; }

        public int[] OptimiseRoute(
            double[,] costMatrix,
            int[] enteredOrder,
            bool fixedEnd = false,
            bool returnToStart = false,
            int? randomSeed = null)
        {
            Called = true;
            FixedEnd = fixedEnd;
            ReturnToStart = returnToStart;

            // append start when returning to start
            return returnToStart
                ? enteredOrder.Concat(new[] { enteredOrder[0] }).ToArray()
                : (int[])enteredOrder.Clone();
        }
    }
}
