using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Contracts.Routes;
using RouteWise.Api.Services;

namespace RouteWise.Tests.Integration;

[TestFixture]
public class RouteEndpointsIntegrationTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        // start application with fake routing and authentication
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "Authentication:AllowedOrigins:0",
                    "http://localhost:5173");

                builder.ConfigureTestServices(services =>
                {
                    // replace Google Routes with fake routing
                    services.RemoveAll<IRoutingService>();
                    services.AddScoped<IRoutingService, FakeRoutingService>();

                    // replace cookie authentication with test authentication
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                    })
                    .AddScheme<AuthenticationSchemeOptions,
                        TestAuthenticationHandler>("Test", _ => { });
                });
            });

        _client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        // satisfy trusted origin middleware
        _client.DefaultRequestHeaders.Add("Origin", "http://localhost:5173");
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
        _factory.Dispose();
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

    private static OptimizeRouteRequest OptimiseRequest(
        string endMode = "open",
        string travelMode = "driving")
    {
        return new OptimizeRouteRequest(
            Start: Point(0),
            Destinations: new List<RoutePointDto>
            {
                Point(1),
                Point(2)
            },
            TravelMode: travelMode,
            Objective: "distance",
            EndMode: endMode,
            End: null,
            ReturnPointId: endMode == "returnToStart"
                ? "return-visit"
                : null);
    }

    [Test]
    public async Task Optimise_ValidRequest_ReturnsOk()
    {
        // send route optimisation request
        var response = await _client.PostAsJsonAsync(
            "/api/routes/optimize",
            OptimiseRequest());

        var result = await response.Content
            .ReadFromJsonAsync<RouteResultDto>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(result, Is.Not.Null);
            Assert.That(result?.Points.Count, Is.EqualTo(3));
            Assert.That(result?.Segments.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Calculate_ValidRequest_PreservesOrder()
    {
        // calculate route without optimisation
        var request = new CalculateRouteRequest(
            new List<RoutePointDto>
            {
                Point(2),
                Point(0),
                Point(1)
            },
            "walking");

        var response = await _client.PostAsJsonAsync(
            "/api/routes/calculate",
            request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<RouteResultDto>();

        Assert.That(
            result?.Points.Select(p => p.Id),
            Is.EqualTo(new[] { "point-2", "point-0", "point-1" }));
    }

    [Test]
    public async Task Optimise_InvalidTravelMode_ReturnsBadRequest()
    {
        // send invalid route settings
        var response = await _client.PostAsJsonAsync(
            "/api/routes/optimize",
            OptimiseRequest(travelMode: "cycling"));

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Optimise_Unauthenticated_ReturnsUnauthorized()
    {
        // disable test authentication for this request
        using var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/routes/optimize");

        request.Content = JsonContent.Create(OptimiseRequest());
        request.Headers.Add("X-Test-Unauthenticated", "true");

        var response = await _client.SendAsync(request);

        Assert.That(
            response.StatusCode,
            Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Optimise_ReturnToStart_UsesReturnOccurrence()
    {
        // optimise route that returns to start
        var response = await _client.PostAsJsonAsync(
            "/api/routes/optimize",
            OptimiseRequest(endMode: "returnToStart"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content
            .ReadFromJsonAsync<RouteResultDto>();

        Assert.Multiple(() =>
        {
            Assert.That(result?.Points.Count, Is.EqualTo(4));
            Assert.That(result?.Points[^1].Id, Is.EqualTo("return-visit"));
            Assert.That(result?.Points[^1].Place.PlaceId,
                Is.EqualTo(result?.Points[0].Place.PlaceId));
        });
    }

    // fake Google routing service
    private sealed class FakeRoutingService : IRoutingService
    {
        public Task<double[,]> GetCostMatrixAsync(
            IReadOnlyList<RoutePointDto> points,
            string travelMode,
            string objective,
            CancellationToken cancellationToken = default)
        {
            // create predictable costs
            var matrix = new double[points.Count, points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                for (int j = 0; j < points.Count; j++)
                {
                    matrix[i, j] = i == j ? 0 : 100;
                }
            }

            return Task.FromResult(matrix);
        }

        public Task<RouteResultDto> CalculateRouteAsync(
            IReadOnlyList<RoutePointDto> points,
            string travelMode,
            CancellationToken cancellationToken = default)
        {
            // create segments in the requested order
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

    // authenticate requests unless explicitly disabled
    private sealed class TestAuthenticationHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // simulate an unauthenticated request
            if (Request.Headers.ContainsKey("X-Test-Unauthenticated"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            // create fake authenticated user
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "test-user"),
                new Claim(ClaimTypes.Name, "Test User")
            };

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
