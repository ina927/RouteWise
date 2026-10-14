using System.Globalization;
using RouteWise.Api.Contracts.Journeys;

namespace RouteWise.Api.Validation;

public static class JourneyValidator
{
    public const int NameLimit = 100;
    public const int NoteLimit = 2000;

    public static string? Validate(CreateJourneyRequest? request)
    {
        if (request is null)
        {
            return "Journey request is required.";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Journey name is required.";
        }

        if (request.Name.Trim().Length > NameLimit)
        {
            return $"Journey name must be {NameLimit} characters or fewer.";
        }

        if (request.Date is not null &&
            !DateOnly.TryParseExact(
                request.Date,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            return "Journey date must be a valid date in YYYY-MM-DD format.";
        }

        if (request.Plan is null)
        {
            return "Journey plan is required.";
        }
        if (request.Plan.TravelMode is not ("walking" or "driving"))
        {
            return "Travel mode must be walking or driving.";
        }

        if (request.Plan.Objective is not ("distance" or "time"))
        {
            return "Optimisation objective must be distance or time.";
        }

        if (request.Plan.EndMode is not ("open" or "returnToStart" or "fixed"))
        {
            return "End mode must be open, returnToStart, or fixed.";
        }
        if (request.Plan.Route is null)
        {
            return "Journey route is required.";
        }

        var points = request.Plan.Route.Points;

        if (points is null)
        {
            return "Route points are required.";
        }

        if (points.Count < 2)
        {
            return "A route must contain at least two location occurrences.";
        }

        if (points.Count > 10)
        {
            return "A route cannot contain more than 10 location occurrences.";
        }

        var pointIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var point in points)
        {
            if (point is null)
            {
                return "Route points cannot contain null entries.";
            }

            if (string.IsNullOrWhiteSpace(point.Id))
            {
                return "Each route point must have an ID.";
            }

            if (!pointIds.Add(point.Id))
            {
                return "Each route point must have a unique ID.";
            }

            if (point.Place is null)
            {
                return "Each route point must contain a place.";
            }

            if (string.IsNullOrWhiteSpace(point.Place.PlaceId))
            {
                return "Each place must have a place ID.";
            }

            if (string.IsNullOrWhiteSpace(point.Place.Name))
            {
                return "Each place must have a name.";
            }

            var coordinates = point.Place.Coordinates;

            if (coordinates is null)
            {
                return "Each place must have coordinates.";
            }

            if (!double.IsFinite(coordinates.Lat) ||
                coordinates.Lat < -90 ||
                coordinates.Lat > 90)
            {
                return "Latitude must be a finite number between -90 and 90.";
            }

            if (!double.IsFinite(coordinates.Lng) ||
                coordinates.Lng < -180 ||
                coordinates.Lng > 180)
            {
                return "Longitude must be a finite number between -180 and 180.";
            }
        }
        if (request.Plan.EndMode == "returnToStart" &&
            points[0].Place.PlaceId != points[^1].Place.PlaceId)
        {
            return "Return to Start must end at the starting place.";
        }

        var segments = request.Plan.Route.Segments;

        if (segments is null)
        {
            return "Route segments are required.";
        }

        if (segments.Count != points.Count - 1)
        {
            return "A route must have one segment between each consecutive pair of points.";
        }

        var segmentIds = new HashSet<string>(StringComparer.Ordinal);
        double calculatedDistance = 0;
        double calculatedDuration = 0;

        for (var index = 0; index < segments.Count; index++)
        {
            var segment = segments[index];

            if (segment is null)
            {
                return "Route segments cannot contain null entries.";
            }

            if (string.IsNullOrWhiteSpace(segment.Id))
            {
                return "Each route segment must have an ID.";
            }

            if (!segmentIds.Add(segment.Id))
            {
                return "Each route segment must have a unique ID.";
            }

            if (segment.FromPointId != points[index].Id ||
                segment.ToPointId != points[index + 1].Id)
            {
                return "Route segments must connect consecutive points in route order.";
            }

            if (!double.IsFinite(segment.DistanceMeters) || segment.DistanceMeters < 0)
            {
                return "Segment distance must be a finite, nonnegative number.";
            }

            if (!double.IsFinite(segment.DurationSeconds) || segment.DurationSeconds < 0)
            {
                return "Segment duration must be a finite, nonnegative number.";
            }

            calculatedDistance += segment.DistanceMeters;
            calculatedDuration += segment.DurationSeconds;

            if (segment.Path is null || segment.Path.Count < 2)
            {
                return "Each segment path must contain at least two coordinates.";
            }

            foreach (var coordinate in segment.Path)
            {
                if (coordinate is null ||
                    !double.IsFinite(coordinate.Lat) || coordinate.Lat < -90 || coordinate.Lat > 90 ||
                    !double.IsFinite(coordinate.Lng) || coordinate.Lng < -180 || coordinate.Lng > 180)
                {
                    return "Segment path coordinates must have finite latitude (-90 to 90) and longitude (-180 to 180).";
                }
            }
        }

        var route = request.Plan.Route;
        if (!double.IsFinite(route.TotalDistanceMeters) || route.TotalDistanceMeters < 0 ||
            !double.IsFinite(route.TotalDurationSeconds) || route.TotalDurationSeconds < 0 ||
            !double.IsFinite(calculatedDistance) || !double.IsFinite(calculatedDuration))
        {
            return "Route totals and segment sums must be finite, nonnegative numbers.";
        }

        const double tolerance = 0.000001;
        if (Math.Abs(route.TotalDistanceMeters - calculatedDistance) > tolerance)
        {
            return "Total distance must equal the sum of segment distances.";
        }

        if (Math.Abs(route.TotalDurationSeconds - calculatedDuration) > tolerance)
        {
            return "Total duration must equal the sum of segment durations.";
        }

        var annotatedPointIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var annotation in request.Annotations ?? [])
        {
            if (annotation is null || string.IsNullOrWhiteSpace(annotation.PointId) ||
                !pointIds.Contains(annotation.PointId))
            {
                return "Each annotation must refer to a point in this route.";
            }

            if (!annotatedPointIds.Add(annotation.PointId))
            {
                return "Only one annotation per route point is allowed.";
            }

            if (annotation.Note?.Trim().Length > NoteLimit)
            {
                return $"Note must be {NoteLimit} characters or fewer.";
            }

            if (annotation.PlannedTime is not null &&
                !TimeOnly.TryParseExact(annotation.PlannedTime, "HH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return "Planned time must be a valid time in HH:mm format.";
            }
        }

        return null;
    }
}
