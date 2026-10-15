
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using RouteWise.Api.Contracts.Journeys;

namespace RouteWise.Api.Services;

// other classes cannot inherit, so no children
public sealed class GoogleRoutesService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    // constructor
    public GoogleRoutesService(
        HttpClient httpClient, // for making HTTP requests
        IConfiguration configuration // for reading settings and User Secrets
        )
        // dependency injection (DI)
    {
        _httpClient = httpClient;

        // read Google API key from User Secrets
        // if null then throw InvalidOperationException
        _apiKey = configuration["GoogleMaps:ApiKey"]
            ?? throw new InvalidOperationException("GoogleMaps:ApiKey is not configured.");
    }

    // GetCostMatrix
    // async means thí can wait for HTTP operations without blocking calling thread
    public async Task<double[,]> GetCostMatrixAsync(
        IReadOnlyList<RoutePointDto> points, // list of route points that thí method will read only
        string travelMode, string objective,
        CancellationToken cancellationToken = default // allows request to be cancelled e.g. if user closes the page while Google running
        )
    {
        // only walking and driving are supported
        ValidateTravelMode(travelMode);

        // objectives can only be distance or time
        if (objective != "distance" && objective != "time")
            throw new ArgumentException("Invalid optimisation objective.");

        // number of points should be between 2 to 10 inclusive
        if (points.Count < 2 || points.Count > 10)
            throw new ArgumentException("Expected 2 to 10 locations.");

        // convert RouteWise point into Google's waypoint format
        var locations = points.Select(p => ToWaypoint(p)).ToArray();

        // anonymous object to convert to JSON when sending request
        // request all pairs for matrix
        var body = new
        {
            origins = locations.Select(location =>
                new { waypoint = location }).ToArray(),
            destinations = locations.Select(location =>
                new { waypoint = location }).ToArray(),
            travelMode = ToGoogleTravelMode(travelMode),

            // ignore live traffic for predictable route costs
            // journeys are not live anyway, can be scheduled
            routingPreference = "TRAFFIC_UNAWARE"
        };

        // HTTP POST request
        // using var ensures request object is disposed when this method finishes
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://routes.googleapis.com/distanceMatrix/v2:computeRouteMatrix");

        // Add API key to header
        request.Headers.Add("X-Goog-Api-Key", _apiKey);

        // field mask to tell Google which response fields we want
        request.Headers.Add(
            "X-Goog-FieldMask",
            "originIndex,destinationIndex," +
            "status,condition," +
            "distanceMeters,duration");

        // convert anonymous object to JSON and attach to HTTP request body
        request.Content = JsonContent.Create(body);

        // send request to Google
        // await means wait for HTTP operation to finish asynchronously
        // response contains HTTP status and response body
        using var response = await _httpClient.SendAsync(
            request, cancellationToken);

        // check whether Google returned HTTP error, if yes then throw exception
        await EnsureGoogleSuccessAsync(response, cancellationToken);

        // Google gives JSON answer, so use JsonDocument to read fields
        // ParseAsync converts response stream into structure to be navigated in C#
        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var matrix = new double[points.Count, points.Count];
        var received = new bool[points.Count, points.Count];

        foreach (var element in document.RootElement.EnumerateArray())
        {
            int from = element.GetProperty("originIndex").GetInt32();
            int to = element.GetProperty("destinationIndex").GetInt32();

            if (from < 0 || from >= points.Count ||
                to < 0 || to >= points.Count ||
                received[from, to])
                throw new InvalidOperationException(
                    "Google returned an invalid matrix element.");

            if (from == to)
            {
                matrix[from, to] = 0;
                received[from, to] = true;
                continue;
            }

            if (!element.TryGetProperty("condition", out var condition) ||
                condition.GetString() != "ROUTE_EXISTS")
                throw new InvalidOperationException($"No route exists between locations {from} and {to}.");

            double cost = objective == "distance"
                ? element.GetProperty("distanceMeters").GetDouble()
                : ParseDuration(element.GetProperty("duration").GetString()!);

            if (!double.IsFinite(cost) || cost < 0)
                throw new InvalidOperationException("Google returned an invalid travel cost.");

            matrix[from, to] = cost;
            received[from, to] = true;
        }

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = 0; j < points.Count; j++)
            {
                if (!received[i, j])
                    throw new InvalidOperationException($"Missing travel cost from {i} to {j}.");
            }
        }

        return matrix;
    }

    public async Task<RouteResultDto> CalculateRouteAsync(
        IReadOnlyList<RoutePointDto> points,
        string travelMode,
        CancellationToken cancellationToken = default)
    {
        ValidateTravelMode(travelMode);

        if (points.Count < 2 || points.Count > 10)
            throw new ArgumentException("Expected 2 to 10 route points.");

        var segments = new List<RouteSegmentDto>();

        for (int i = 0; i < points.Count - 1; i++)
        {
            var from = points[i];
            var to = points[i + 1];

            // A return leg can refer to the same physical place.
            if (from.Place.Coordinates == to.Place.Coordinates)
            {
                segments.Add(new RouteSegmentDto(
                    $"segment:{Uri.EscapeDataString(from.Id)}:{Uri.EscapeDataString(to.Id)}",
                    from.Id,
                    to.Id,
                    0,
                    0,
                    new List<CoordinatesDto>
                    {
                        from.Place.Coordinates,
                        to.Place.Coordinates
                    }));
                continue;
            }

            var body = new
            {
                origin = ToWaypoint(from),
                destination = ToWaypoint(to),
                travelMode = ToGoogleTravelMode(travelMode),
                routingPreference = "TRAFFIC_UNAWARE",
                polylineQuality = "HIGH_QUALITY",
                polylineEncoding = "ENCODED_POLYLINE"
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://routes.googleapis.com/directions/v2:computeRoutes");

            request.Headers.Add("X-Goog-Api-Key", _apiKey);
            request.Headers.Add(
                "X-Goog-FieldMask",
                "routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline");

            request.Content = JsonContent.Create(body);

            using var response = await _httpClient.SendAsync(
                request, cancellationToken);

            await EnsureGoogleSuccessAsync(response, cancellationToken);

            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            var routes = document.RootElement.GetProperty("routes");

            if (routes.GetArrayLength() == 0)
                throw new InvalidOperationException(
                    "Google could not calculate this route.");

            var route = routes[0];

            double distance = route.GetProperty("distanceMeters").GetDouble();
            double duration = ParseDuration(
                route.GetProperty("duration").GetString()!);

            string encoded = route.GetProperty("polyline")
                .GetProperty("encodedPolyline").GetString()!;

            var path = DecodePolyline(encoded);

            if (path.Count < 2)
                throw new InvalidOperationException("Google returned invalid route geometry.");

            segments.Add(new RouteSegmentDto(
                $"segment:{Uri.EscapeDataString(from.Id)}:{Uri.EscapeDataString(to.Id)}",
                from.Id,
                to.Id,
                distance,
                duration,
                path));
        }

        return new RouteResultDto(
            points.ToList(),
            segments,
            segments.Sum(segment => segment.DistanceMeters),
            segments.Sum(segment => segment.DurationSeconds));
    }

    private static object ToWaypoint(RoutePointDto point)
    {
        var coordinates = point.Place.Coordinates;

        if (!double.IsFinite(coordinates.Lat) ||
            !double.IsFinite(coordinates.Lng) ||
            coordinates.Lat < -90 || coordinates.Lat > 90 ||
            coordinates.Lng < -180 || coordinates.Lng > 180)
            throw new ArgumentException("Invalid coordinates.");

        return new
        {
            location = new
            {
                latLng = new
                {
                    latitude = coordinates.Lat,
                    longitude = coordinates.Lng
                }
            }
        };
    }

    private static void ValidateTravelMode(string mode)
    {
        if (mode != "walking" && mode != "driving")
            throw new ArgumentException("Invalid travel mode.");
    }

    private static string ToGoogleTravelMode(string mode)
    {
        return mode == "walking" ? "WALK" : "DRIVE";
    }

    private static double ParseDuration(string value)
    {
        if (!value.EndsWith('s') ||
            !double.TryParse(
                value[..^1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double seconds))
            throw new InvalidOperationException(
                "Google returned an invalid duration.");

        return seconds;
    }

    private static async Task EnsureGoogleSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string details = await response.Content.ReadAsStringAsync(
            cancellationToken);

        throw new HttpRequestException(
            $"Google Routes API returned HTTP {(int)response.StatusCode}: {details}",
            null,
            response.StatusCode);
    }

    private static List<CoordinatesDto> DecodePolyline(string encoded)
    {
        var points = new List<CoordinatesDto>();
        int index = 0;
        int latitude = 0;
        int longitude = 0;

        while (index < encoded.Length)
        {
            latitude += DecodeValue(encoded, ref index);
            longitude += DecodeValue(encoded, ref index);

            points.Add(new CoordinatesDto(
                latitude / 100000.0,
                longitude / 100000.0));
        }

        return points;
    }

    private static int DecodeValue(string encoded, ref int index)
    {
        int result = 0;
        int shift = 0;
        int value;

        do
        {
            if (index >= encoded.Length || shift > 30)
                throw new InvalidOperationException("Invalid encoded polyline.");

            value = encoded[index++] - 63;
            result |= (value & 0x1f) << shift;
            shift += 5;
        }
        while (value >= 0x20);

        return (result & 1) != 0
            ? ~(result >> 1)
            : result >> 1;
    }
}
