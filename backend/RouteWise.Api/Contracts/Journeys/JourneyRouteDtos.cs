using System.Text.Json.Serialization;

namespace RouteWise.Api.Contracts.Journeys;

public sealed record CoordinatesDto(
    double Lat,
    double Lng);

public sealed record PlaceDto(
    string PlaceId,
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Address,
    CoordinatesDto Coordinates);

public sealed record RoutePointDto(
    string Id,
    PlaceDto Place);

public sealed record RouteSegmentDto(
    string Id,
    string FromPointId,
    string ToPointId,
    double DistanceMeters,
    double DurationSeconds,
    List<CoordinatesDto> Path);

public sealed record RouteResultDto(
    List<RoutePointDto> Points,
    List<RouteSegmentDto> Segments,
    double TotalDistanceMeters,
    double TotalDurationSeconds);

public sealed record JourneyPlanDto(
    string TravelMode,
    string Objective,
    string EndMode,
    RouteResultDto Route);

public sealed record PointAnnotationDto(
    string PointId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Note,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PlannedTime);
