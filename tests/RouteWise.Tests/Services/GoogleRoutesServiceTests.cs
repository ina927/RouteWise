using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Services;

namespace RouteWise.Tests.Services;

[TestFixture]
public class GoogleRoutesServiceTests
{
    // 2 example locations used throughout the tests
    // not real route measurements
    private static List<RoutePointDto> CreatePoints()
    {
        return new List<RoutePointDto>
        {
            new(
                "start",
                new PlaceDto(
                    "place-a",
                    "UTS Tower",
                    null,
                    new CoordinatesDto(-33.8836, 151.2008))),

            new(
                "end",
                new PlaceDto(
                    "place-b",
                    "Bondi Beach",
                    null,
                    new CoordinatesDto(-33.8915, 151.2767)))
        };
    }

    // a successful 2 x 2 Google Route Matrix response
    // distance (m)
    //        A      B
    // A      0    8100
    // B   9200       0
    // time (s)
    //        A      B
    // A      0     960
    // B   1020       0
    // 2 directions have different costs to test an asymmetric matrix
    // should not assume that the 2 directions have the same cost due to road restrictions
    private const string MatrixResponse = """
        [
          {
            "originIndex": 0,
            "destinationIndex": 0,
            "condition": "ROUTE_EXISTS",
            "distanceMeters": 0,
            "duration": "0s"
          },
          {
            "originIndex": 0,
            "destinationIndex": 1,
            "condition": "ROUTE_EXISTS",
            "distanceMeters": 8100,
            "duration": "960s"
          },
          {
            "originIndex": 1,
            "destinationIndex": 0,
            "condition": "ROUTE_EXISTS",
            "distanceMeters": 9200,
            "duration": "1020s"
          },
          {
            "originIndex": 1,
            "destinationIndex": 1,
            "condition": "ROUTE_EXISTS",
            "distanceMeters": 0,
            "duration": "0s"
          }
        ]
        """;

    // Example response for Google's Compute Routes API test for distance, duration, and polyline parsing
    // The encoded polyline is a standard example with 3 points, not a real route
    private const string RouteResponse = """
        {
          "routes": [
            {
              "distanceMeters": 8100,
              "duration": "960s",
              "polyline": {
                "encodedPolyline": "_p~iF~ps|U_ulLnnqC_mqNvxq`@"
              }
            }
          ]
        }
        """;

    // Build a real GoogleRoutesService but replace its HTTP connection with our fake handler
    // The fake API key is only used in unit tests
    // each test controls API response without contacting Google with a real key
    private static GoogleRoutesService CreateService(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
    {
        var handler = new FakeHttpMessageHandler(responder);

        var httpClient = new HttpClient(handler);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["GoogleMaps:ApiKey"] = "fake-test-key"
                })
            .Build();

        return new GoogleRoutesService(httpClient, configuration);
    }

    // FakeHttpMessageHandler intercepts HTTP requests.
    // No actual network connection is made.
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            Task<HttpResponseMessage>> _responder;

        public FakeHttpMessageHandler(
            Func<
                HttpRequestMessage,
                Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _responder(request);
        }
    }

    // Helper to construct an HTTP response containing JSON.
    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json")
        };
    }

    // A distance objective should return distanceMeters
    [Test]
    public async Task GetCostMatrix_Distance_ReturnsDistances()
    {
        var service = CreateService(request =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.RequestUri!.AbsolutePath,
                Does.Contain("computeRouteMatrix"));

            return Task.FromResult(JsonResponse(MatrixResponse));
        });

        var matrix = await service.GetCostMatrixAsync(
            CreatePoints(),
            "driving",
            "distance");

        Assert.Multiple(() =>
        {
            Assert.That(matrix[0, 0], Is.EqualTo(0));
            Assert.That(matrix[0, 1], Is.EqualTo(8100));
            Assert.That(matrix[1, 0], Is.EqualTo(9200));
            Assert.That(matrix[1, 1], Is.EqualTo(0));
        });
    }

    // A time objective should return duration in seconds.
    [Test]
    public async Task GetCostMatrix_Time_ReturnsSeconds()
    {
        var service = CreateService(request =>
            Task.FromResult(JsonResponse(MatrixResponse)));

        var matrix = await service.GetCostMatrixAsync(
            CreatePoints(),
            "walking",
            "time");

        Assert.That(matrix[0, 1], Is.EqualTo(960));
        Assert.That(matrix[1, 0], Is.EqualTo(1020));
    }

    // Walking must use WALK and omit routingPreference as it's driving-specific
    [Test]
    public async Task GetCostMatrix_Walking_OmitsRoutingPreference()
    {
        string? capturedBody = null;

        var service = CreateService(async request =>
        {
            capturedBody = await request.Content!
                .ReadAsStringAsync();

            return JsonResponse(MatrixResponse);
        });

        await service.GetCostMatrixAsync(
            CreatePoints(),
            "walking",
            "distance");

        using var json = JsonDocument.Parse(capturedBody!);
        var root = json.RootElement;

        Assert.That(root.GetProperty("travelMode").GetString(),
            Is.EqualTo("WALK"));

        Assert.That(root.TryGetProperty(
            "routingPreference", out _), Is.False);
    }

    // Driving must use DRIVE and routingPreference TRAFFIC_UNAWARE
    [Test]
    public async Task GetCostMatrix_Driving_UsesTrafficUnaware()
    {
        string? capturedBody = null;

        var service = CreateService(async request =>
        {
            capturedBody = await request.Content!
                .ReadAsStringAsync();

            return JsonResponse(MatrixResponse);
        });

        await service.GetCostMatrixAsync(
            CreatePoints(),
            "driving",
            "distance");

        using var json = JsonDocument.Parse(capturedBody!);
        var root = json.RootElement;

        Assert.That(root.GetProperty("travelMode").GetString(),
            Is.EqualTo("DRIVE"));

        Assert.That(
            root.GetProperty("routingPreference").GetString(),
            Is.EqualTo("TRAFFIC_UNAWARE"));
    }

    // Reject unsupported travel modes before making an HTTP call
    [Test]
    public void GetCostMatrix_InvalidTravelMode_Throws()
    {
        var service = CreateService(request =>
            throw new InvalidOperationException("HTTP should not be called."));

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.GetCostMatrixAsync(
                CreatePoints(),
                "cycling",
                "distance"));
    }

    // Reject unsupported optimisation objectives
    [Test]
    public void GetCostMatrix_InvalidObjective_Throws()
    {
        var service = CreateService(request =>
            throw new InvalidOperationException("HTTP should not be called."));

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.GetCostMatrixAsync(
                CreatePoints(),
                "driving",
                "cost"));
    }

    // Every origin/destination pair must be returned
    [Test]
    public void GetCostMatrix_MissingElement_Throws()
    {
        const string incompleteMatrix = """
            [
              {
                "originIndex": 0,
                "destinationIndex": 0
              }
            ]
            """;

        var service = CreateService(request =>
            Task.FromResult(JsonResponse(incompleteMatrix)));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetCostMatrixAsync(
                CreatePoints(),
                "driving",
                "distance"));
    }

    // An element-level Google API error must be detected
    [Test]
    public void GetCostMatrix_ElementError_Throws()
    {
        const string errorMatrix = """
            [
              {
                "originIndex": 0,
                "destinationIndex": 0,
                "status": {
                  "code": 3,
                  "message": "Invalid waypoint"
                }
              }
            ]
            """;

        var service = CreateService(request =>
            Task.FromResult(JsonResponse(errorMatrix)));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetCostMatrixAsync(
                CreatePoints(),
                "driving",
                "distance"));
    }

    // The Compute Routes response should produce a segment with the expected distance, duration, and decoded path
    [Test]
    public async Task CalculateRoute_ValidResponse_ReturnsSegment()
    {
        var service = CreateService(request =>
            Task.FromResult(JsonResponse(RouteResponse)));

        var result = await service.CalculateRouteAsync(
            CreatePoints(),
            "driving");

        Assert.That(result.Segments, Has.Count.EqualTo(1));

        var segment = result.Segments[0];

        Assert.Multiple(() =>
        {
            Assert.That(segment.FromPointId, Is.EqualTo("start"));
            Assert.That(segment.ToPointId, Is.EqualTo("end"));
            Assert.That(segment.DistanceMeters, Is.EqualTo(8100));
            Assert.That(segment.DurationSeconds, Is.EqualTo(960));
            Assert.That(segment.Path, Has.Count.EqualTo(3));
        });
    }

    // Walking requests to Compute Routes must omit routingPreference as it's driving-specific
    [Test]
    public async Task CalculateRoute_Walking_OmitsRoutingPreference()
    {
        string? capturedBody = null;

        var service = CreateService(async request =>
        {
            capturedBody = await request.Content!
                .ReadAsStringAsync();

            return JsonResponse(RouteResponse);
        });

        await service.CalculateRouteAsync(
            CreatePoints(),
            "walking");

        using var json = JsonDocument.Parse(capturedBody!);

        Assert.That(
            json.RootElement.GetProperty("travelMode").GetString(),
            Is.EqualTo("WALK"));

        Assert.That(
            json.RootElement.TryGetProperty(
                "routingPreference", out _),
            Is.False);
    }

    // If 2 consecutive points have identical coordinates then the service should not call Google
    [Test]
    public async Task CalculateRoute_SameCoordinates_SkipsHttp()
    {
        var points = CreatePoints();

        points[1] = new RoutePointDto(
            "end",
            new PlaceDto(
                "place-b",
                "Bondi Beach",
                null,
                points[0].Place.Coordinates));

        var service = CreateService(request =>
            throw new InvalidOperationException("HTTP should not be called."));

        var result = await service.CalculateRouteAsync(
            points,
            "walking");

        Assert.That(result.Segments, Has.Count.EqualTo(1));
        Assert.That(result.Segments[0].DistanceMeters,
            Is.EqualTo(0));
        Assert.That(result.Segments[0].DurationSeconds,
            Is.EqualTo(0));
    }

    // An HTTP error from Google should produce an exception
    [Test]
    public void CalculateRoute_HttpError_Throws()
    {
        var service = CreateService(request =>
            Task.FromResult(JsonResponse(
                """{"error":"API key rejected"}""",
                HttpStatusCode.Forbidden)));

        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await service.CalculateRouteAsync(
                CreatePoints(),
                "driving"));
    }

    // If Google returns no routes, calculation should fail
    [Test]
    public void CalculateRoute_NoRoutes_Throws()
    {
        var service = CreateService(request =>
            Task.FromResult(JsonResponse(
                """{"routes":[]}""")));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.CalculateRouteAsync(
                CreatePoints(),
                "driving"));
    }
}
