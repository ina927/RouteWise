using System.Globalization;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Models;

namespace RouteWise.Api.Mapping;

public static class JourneyMapper
{
    // Validate the request before mapping. This method does not save or verify road costs.
    public static Journey ToEntity(CreateJourneyRequest request, string ownerId)
    {
        var plan = request.Plan!;
        var annotations = (request.Annotations ?? [])
            .ToDictionary(annotation => annotation.PointId, StringComparer.Ordinal);

        var journey = new Journey
        {
            OwnerId = ownerId,
            Name = request.Name!.Trim(),
            Date = request.Date is null ? null
                : DateOnly.ParseExact(request.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            TravelMode = ToTravelMode(plan.TravelMode),
            Objective = ToObjective(plan.Objective),
            EndMode = ToEndMode(plan.EndMode)
        };

        journey.Points = plan.Route.Points.Select((point, index) =>
        {
            annotations.TryGetValue(point.Id, out var annotation);
            return new JourneyPoint
            {
                JourneyId = journey.Id,
                Id = point.Id,
                Position = index,
                PlaceId = point.Place.PlaceId,
                Name = point.Place.Name,
                Address = point.Place.Address,
                Latitude = point.Place.Coordinates.Lat,
                Longitude = point.Place.Coordinates.Lng,
                Note = annotation?.Note,
                PlannedTime = annotation?.PlannedTime is null ? null
                    : TimeOnly.ParseExact(annotation.PlannedTime, "HH:mm", CultureInfo.InvariantCulture)
            };
        }).ToList();

        journey.Segments = plan.Route.Segments.Select((segment, index) => new JourneySegment
        {
            JourneyId = journey.Id,
            Id = segment.Id,
            Position = index,
            FromPointId = segment.FromPointId,
            ToPointId = segment.ToPointId,
            DistanceMeters = segment.DistanceMeters,
            DurationSeconds = segment.DurationSeconds,
            Path = segment.Path.Select(coordinate => new PathCoordinate
            {
                Lat = coordinate.Lat,
                Lng = coordinate.Lng
            }).ToList()
        }).ToList();

        return journey;
    }

    // Owner, Points and Segments must be loaded. The caller checks permissions.
    public static JourneyResponse ToResponse(Journey journey, string access)
    {
        if (access is not ("owner" or "shared"))
            throw new ArgumentException("Access must be owner or shared.", nameof(access));

        var points = journey.Points.OrderBy(point => point.Position).ToList();
        var segments = journey.Segments.OrderBy(segment => segment.Position).ToList();

        var route = new RouteResultDto(
            points.Select(point => new RoutePointDto(point.Id,
                new PlaceDto(point.PlaceId, point.Name, point.Address,
                    new CoordinatesDto(point.Latitude, point.Longitude)))).ToList(),
            segments.Select(segment => new RouteSegmentDto(
                segment.Id, segment.FromPointId, segment.ToPointId,
                segment.DistanceMeters, segment.DurationSeconds,
                segment.Path.Select(coordinate => new CoordinatesDto(coordinate.Lat, coordinate.Lng)).ToList())).ToList(),
            segments.Sum(segment => segment.DistanceMeters),
            segments.Sum(segment => segment.DurationSeconds));

        var travelMode = journey.TravelMode switch
        {
            TravelMode.Walking => "walking",
            TravelMode.Driving => "driving",
            _ => throw new InvalidOperationException("Unsupported stored travel mode.")
        };
        var objective = journey.Objective switch
        {
            OptimisationObjective.Distance => "distance",
            OptimisationObjective.Time => "time",
            _ => throw new InvalidOperationException("Unsupported stored optimisation objective.")
        };
        var endMode = journey.EndMode switch
        {
            EndMode.Open => "open",
            EndMode.ReturnToStart => "returnToStart",
            EndMode.Fixed => "fixed",
            _ => throw new InvalidOperationException("Unsupported stored end mode.")
        };

        var annotations = points
            .Where(point => point.Note is not null || point.PlannedTime is not null)
            .Select(point => new PointAnnotationDto(point.Id, point.Note,
                point.PlannedTime?.ToString("HH:mm", CultureInfo.InvariantCulture)))
            .ToList();

        return new JourneyResponse(journey.Id,
            new JourneyOwnerDto(journey.Owner.Id, journey.Owner.Username), journey.Name,
            journey.Date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            new JourneyPlanDto(travelMode, objective, endMode, route), annotations, access);
    }

    public static JourneySummaryResponse ToSummary(
        Journey journey,
        string representativePlaceId)
    {
        var points = journey.Points
            .OrderBy(point => point.Position)
            .ToList();

        if (points.Count < 2)
        {
            throw new InvalidOperationException(
                "A saved journey must contain at least two points.");
        }

        var start = points[0];
        var end = points[^1];

        var travelMode = journey.TravelMode switch
        {
            TravelMode.Walking => "walking",
            TravelMode.Driving => "driving",
            _ => throw new InvalidOperationException(
                "Unsupported stored travel mode.")
        };

        return new JourneySummaryResponse(
            journey.Id,
            new JourneyOwnerDto(
                journey.Owner.Id,
                journey.Owner.Username),
            journey.Name,
            journey.Date?.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture),
            representativePlaceId,
            travelMode,
            new PlaceSummaryDto(start.PlaceId, start.Name),
            new PlaceSummaryDto(end.PlaceId, end.Name),
            points.Count,
            journey.Segments.Sum(segment => segment.DistanceMeters),
            journey.Segments.Sum(segment => segment.DurationSeconds));
    }

    public static TravelMode ToTravelMode(string value) =>
        value switch
        {
            "walking" => TravelMode.Walking,
            "driving" => TravelMode.Driving,
            _ => throw new ArgumentException(
                "Unsupported travel mode.", nameof(value))
        };

    public static OptimisationObjective ToObjective(string value) =>
        value switch
        {
            "distance" => OptimisationObjective.Distance,
            "time" => OptimisationObjective.Time,
            _ => throw new ArgumentException(
                "Unsupported optimisation objective.", nameof(value))
        };

    public static EndMode ToEndMode(string value) =>
        value switch
        {
            "open" => EndMode.Open,
            "returnToStart" => EndMode.ReturnToStart,
            "fixed" => EndMode.Fixed,
            _ => throw new ArgumentException(
                "Unsupported end mode.", nameof(value))
        };
}
