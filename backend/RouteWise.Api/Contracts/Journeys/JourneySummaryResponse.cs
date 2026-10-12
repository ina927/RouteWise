using System.Text.Json.Serialization;

namespace RouteWise.Api.Contracts.Journeys;

public sealed record PlaceSummaryDto(
    string PlaceId,
    string Name);

public sealed record JourneySummaryResponse(
    string Id,
    JourneyOwnerDto Owner,
    string Name,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Date,

    string RepresentativePlaceId,
    string TravelMode,
    PlaceSummaryDto Start,
    PlaceSummaryDto End,
    int LocationCount,
    double TotalDistanceMeters,
    double TotalDurationSeconds);