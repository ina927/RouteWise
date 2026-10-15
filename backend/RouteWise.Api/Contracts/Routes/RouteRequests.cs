using RouteWise.Api.Contracts.Journeys;

namespace RouteWise.Api.Contracts.Routes;

// request for route optimisation
public sealed record OptimizeRouteRequest(
    RoutePointDto Start,
    List<RoutePointDto> Destinations,
    string TravelMode,
    string Objective,
    string EndMode,
    RoutePointDto? End = null,
    string? ReturnPointId = null
);

// request for calculating a route in the given order
public sealed record CalculateRouteRequest(
    List<RoutePointDto> Points,
    string TravelMode
);
