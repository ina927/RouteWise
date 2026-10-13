using System.Text.Json.Serialization;

namespace RouteWise.Api.Contracts.Journeys;

public sealed record JourneyOwnerDto(
    string Id,
    string Username);

public sealed record JourneyResponse(
    string Id,
    JourneyOwnerDto Owner,
    string Name,

    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Date,

    JourneyPlanDto Plan,
    List<PointAnnotationDto> Annotations,
    string Access);