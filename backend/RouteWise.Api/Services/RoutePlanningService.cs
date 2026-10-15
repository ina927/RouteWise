using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Contracts.Routes;

namespace RouteWise.Api.Services;

public sealed class RoutePlanningService
{
    private readonly IRoutingService _routingService;
    private readonly IRouteOptimiser _optimiser;

    public RoutePlanningService(
        IRoutingService routingService,
        IRouteOptimiser optimiser)
    {
        _routingService = routingService;
        _optimiser = optimiser;
    }

    public async Task<RouteResultDto> OptimiseAsync(
        OptimizeRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // validate route settings
        if (request.TravelMode is not ("walking" or "driving"))
        {
            throw new ArgumentException("Invalid travel mode.");
        }

        if (request.Objective is not ("distance" or "time"))
        {
            throw new ArgumentException("Invalid optimisation objective.");
        }

        if (request.EndMode is not ("open" or "fixed" or "returnToStart"))
        {
            throw new ArgumentException("Invalid end mode.");
        }

        if (request.Start is null || request.Destinations is null)
        {
            throw new ArgumentException("Start and destinations are required.");
        }

        // collect entered locations
        var points = new List<RoutePointDto> { request.Start };
        points.AddRange(request.Destinations);

        // include the fixed end
        if (request.EndMode == "fixed")
        {
            if (request.End is null)
            {
                throw new ArgumentException("A fixed end point is required.");
            }

            points.Add(request.End);
        }

        // validate number of entered locations
        if (points.Count < 2 || points.Count > 10)
        {
            throw new ArgumentException("Enter between 2 and 10 locations.");
        }

        // validate point IDs
        if (points.Any(p => p is null || string.IsNullOrWhiteSpace(p.Id)))
        {
            throw new ArgumentException("Each location must have a valid point ID.");
        }

        // reject duplicate point IDs
        if (points.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count()
            != points.Count)
        {
            throw new ArgumentException("Each location must have a unique point ID.");
        }

        // validate the return point ID
        if (request.EndMode == "returnToStart")
        {
            if (string.IsNullOrWhiteSpace(request.ReturnPointId))
                throw new ArgumentException("A return point ID is required.");

            if (points.Any(p => p.Id == request.ReturnPointId))
                throw new ArgumentException("The return point ID must be unique.");
        }

        // get cost matrix from Google Routes
        double[,] costMatrix = await _routingService.GetCostMatrixAsync(
            points,
            request.TravelMode,
            request.Objective,
            cancellationToken);

        // create original order: 0, 1, 2, ...
        int[] enteredOrder = Enumerable.Range(0, points.Count).ToArray();

        // optimise visiting order
        int[] optimisedOrder = _optimiser.OptimiseRoute(
            costMatrix,
            enteredOrder,
            fixedEnd: request.EndMode == "fixed",
            returnToStart: request.EndMode == "returnToStart");

        // convert indices back into route points
        var orderedPoints = optimisedOrder
            .Select(index => points[index])
            .ToList();

        // use a separate occurrence ID for the return visit
        if (request.EndMode == "returnToStart")
        {
            orderedPoints[^1] = new RoutePointDto(
                request.ReturnPointId!,
                request.Start.Place);
        }

        // calculate final route geometry and totals
        return await _routingService.CalculateRouteAsync(
            orderedPoints,
            request.TravelMode,
            cancellationToken);
    }


    public async Task<RouteResultDto> CalculateAsync(
        CalculateRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // validate travel mode
        if (request.TravelMode is not ("walking" or "driving"))
        {
            throw new ArgumentException("Invalid travel mode.");
        }

        // validate number of points
        if (request.Points is null ||
            request.Points.Count < 2 ||
            request.Points.Count > 11)
        {
            throw new ArgumentException("A route must contain between 2 and 11 point occurrences.");
        }

        // validate point IDs
        if (request.Points.Any(p =>
            p is null || string.IsNullOrWhiteSpace(p.Id)))
        {
            throw new ArgumentException("Each location must have a valid point ID.");
        }

        // reject duplicate point IDs
        if (request.Points.Select(p => p.Id)
            .Distinct(StringComparer.Ordinal).Count() != request.Points.Count)
        {
            throw new ArgumentException("Each location must have a unique point ID.");
        }

        // validate place information
        if (request.Points.Any(p =>
            p.Place is null ||
            string.IsNullOrWhiteSpace(p.Place.PlaceId) ||
            p.Place.Coordinates is null))
        {
            throw new ArgumentException("Each location must have valid place information.");
        }

        // validate coordinates
        foreach (var point in request.Points)
        {
            double lat = point.Place.Coordinates.Lat;
            double lng = point.Place.Coordinates.Lng;

            if (!double.IsFinite(lat) || lat < -90 || lat > 90 ||
                !double.IsFinite(lng) || lng < -180 || lng > 180)
            {
                throw new ArgumentException("Location coordinates must be valid.");
            }
        }

        // calculate route without changing point order
        return await _routingService.CalculateRouteAsync(
            request.Points,
            request.TravelMode,
            cancellationToken);
    }
}
