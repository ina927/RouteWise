namespace RouteWise.Api.Contracts.Journeys;

public sealed record CreateJourneyRequest(
    string? Name,
    string? Date,
    JourneyPlanDto? Plan,
    List<PointAnnotationDto>? Annotations);