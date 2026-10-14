using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Data;
using RouteWise.Api.Mapping;
using RouteWise.Api.Models;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Validation;
using RouteWise.Api.Services;

namespace RouteWise.Api.Endpoints;

public static class JourneyEndpoints
{
    public static void MapJourneyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/journeys", GetOwnedJourneysAsync).RequireAuthorization();
        app.MapGet("/api/journeys/{id}", GetJourneyAsync).RequireAuthorization();

        app.MapPost("/api/journeys", CreateJourneyAsync).RequireAuthorization();
        app.MapPatch("/api/journeys/{id}", UpdateJourneyAsync).RequireAuthorization();
        app.MapDelete("/api/journeys/{id}", DeleteJourneyAsync).RequireAuthorization();
        app.MapPost("/api/journeys/{id}/duplicate", DuplicateJourneyAsync).RequireAuthorization();
    }

    private static async Task<IResult> GetOwnedJourneysAsync(HttpContext context,
        RouteWiseDbContext db, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var journeys = await db.Journeys.AsNoTracking()
            .Where(journey => journey.OwnerId == userId)
            .Include(journey => journey.Owner)
            .Include(journey => journey.Points)
            .Include(journey => journey.Segments)
            .ToListAsync(cancellationToken);

        // Match the mock's first-stop photo choice until the team settles the policy.
        var summaries = journeys.Select(journey => JourneyMapper.ToSummary(journey,
            journey.Points.OrderBy(point => point.Position).First().PlaceId)).ToList();

        return Results.Ok(summaries);
    }

    private static async Task<IResult> GetJourneyAsync(
        string id, HttpContext context,
        IJourneyService service, 
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Results.Json(new { message = "Authentication required." },
                statusCode: StatusCodes.Status401Unauthorized);

        var result = await service.GetAsync(id, userId, cancellationToken);
        if (result.IsSuccess)
            return Results.Ok(result.Value!);

        return result.Error switch
        {
            ServiceError.NotFound => Results.NotFound(new { message = result.Message }),
            ServiceError.Forbidden => Results.Json(new { message = result.Message },
                statusCode: StatusCodes.Status403Forbidden),
            _ => throw new InvalidOperationException("Unexpected result from journey lookup.")
        };
    }

    private static async Task<IResult> CreateJourneyAsync(
        CreateJourneyRequest request,
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        // Check the submitted journey data.
        var error = JourneyValidator.Validate(request);
        if (error is not null)
            return Results.BadRequest(new { message = error });

        // Find the logged-in user
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var owner = await db.Users.SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

        if (owner is null)
            return Results.Json(
                new { message = "Authentication required." },
                statusCode: StatusCodes.Status401Unauthorized);
            
        // Convert the request into database models
        var journey = JourneyMapper.ToEntity(request, owner.Id);
        journey.Owner = owner;

        db.Journeys.Add(journey);
        await db.SaveChangesAsync(cancellationToken);

        // Save the journey and its related points and segments
        var response = JourneyMapper.ToResponse(journey, "owner");

        return Results.Created($"/api/journeys/{journey.Id}", response);

    }
    private static async Task<IResult> UpdateJourneyAsync(
        string id,
        UpdateJourneyRequest request,
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        // Load the journey and the information needed for its response.
        var journey = await db.Journeys
            .Include(journey => journey.Owner)
            .Include(journey => journey.Points)
            .Include(journey => journey.Segments)
            .SingleOrDefaultAsync(
                journey => journey.Id == id,
                cancellationToken);

        if (journey is null)
            return Results.NotFound(
                new { message = "Journey not found." });

        // Only the owner can make changes.
        if (journey.OwnerId != userId)
            return Results.Json(
                new { message = "Access denied." },
                statusCode: StatusCodes.Status403Forbidden);

        // Omitted fields retain their current values. Validate before mutation.
        var updatedName = journey.Name;
        var updatedDate = journey.Date;

        if (request.Name.ValueKind != JsonValueKind.Undefined)
        {
            if (request.Name.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(request.Name.GetString()))
                return Results.BadRequest(new { message = "Journey name is required." });

            updatedName = request.Name.GetString()!.Trim();
        }

        if (request.Date.ValueKind == JsonValueKind.Null)
        {
            updatedDate = null;
        }
        else if (request.Date.ValueKind != JsonValueKind.Undefined)
        {
            if (request.Date.ValueKind != JsonValueKind.String ||
                !DateOnly.TryParseExact(request.Date.GetString(), "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                return Results.BadRequest(new
                {
                    message = "Journey date must be a valid date in YYYY-MM-DD format."
                });

            updatedDate = parsedDate;
        }

        var snapshot = JourneyMapper.ToResponse(journey, "owner");
        var updatedPlan = snapshot.Plan;

        var replacingPlan =
            request.Plan.ValueKind != JsonValueKind.Undefined;

        if (replacingPlan)
        {
            if (request.Plan.ValueKind != JsonValueKind.Object)
                return Results.BadRequest(
                    new { message = "Plan must be an object." });

            try
            {
                updatedPlan = request.Plan.Deserialize<JourneyPlanDto>(
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            }
            catch (JsonException)
            {
                return Results.BadRequest(
                    new { message = "Invalid journey plan." });
            }
        }

        var proposedJourney = new CreateJourneyRequest(updatedName,
            updatedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), updatedPlan, null);
        var planError = JourneyValidator.Validate(proposedJourney);
        if (planError is not null)
            return Results.BadRequest(new { message = planError });

        var updatedAnnotations = snapshot.Annotations;
        if (request.Annotations.ValueKind != JsonValueKind.Undefined)
        {
            if (request.Annotations.ValueKind != JsonValueKind.Array)
                return Results.BadRequest(new { message = "Annotations must be an array." });

            try
            {
                updatedAnnotations = request.Annotations.Deserialize<List<PointAnnotationDto>>(
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { message = "Invalid annotation data." });
            }
        }
        else if (replacingPlan)
        {
            var survivingPointIds = updatedPlan.Route.Points.Select(point => point.Id).ToHashSet();
            updatedAnnotations = snapshot.Annotations
                .Where(annotation => survivingPointIds.Contains(annotation.PointId)).ToList();
        }

        var validationRequest = new CreateJourneyRequest(updatedName,
            updatedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), updatedPlan, updatedAnnotations);
        var error = JourneyValidator.Validate(validationRequest);
        if (error is not null)
            return Results.BadRequest(new { message = error });

        if (replacingPlan)
        {
            var replacement = JourneyMapper.ToEntity(validationRequest, journey.OwnerId);
            foreach (var point in replacement.Points)
                point.JourneyId = journey.Id;
            foreach (var segment in replacement.Segments)
                segment.JourneyId = journey.Id;

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // Remove dependent segments first; saved deletions detach old entities
            // before replacement objects reuse their journey-scoped keys.
            db.JourneySegments.RemoveRange(journey.Segments);
            await db.SaveChangesAsync(cancellationToken);
            journey.Segments.Clear();
            db.JourneyPoints.RemoveRange(journey.Points);
            await db.SaveChangesAsync(cancellationToken);
            journey.Points.Clear();

            journey.Name = replacement.Name;
            journey.Date = replacement.Date;
            journey.TravelMode = replacement.TravelMode;
            journey.Objective = replacement.Objective;
            journey.EndMode = replacement.EndMode;
            journey.Points = replacement.Points;
            journey.Segments = replacement.Segments;
            db.JourneyPoints.AddRange(journey.Points);
            db.JourneySegments.AddRange(journey.Segments);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            journey.Name = updatedName;
            journey.Date = updatedDate;
            if (request.Annotations.ValueKind != JsonValueKind.Undefined)
            {
                var annotationsByPoint = updatedAnnotations.ToDictionary(annotation => annotation.PointId);
                foreach (var point in journey.Points)
                {
                    annotationsByPoint.TryGetValue(point.Id, out var annotation);
                    point.Note = annotation?.Note;
                    point.PlannedTime = annotation?.PlannedTime is null ? null
                        : TimeOnly.ParseExact(annotation.PlannedTime, "HH:mm", CultureInfo.InvariantCulture);
                }
            }
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(JourneyMapper.ToResponse(journey, "owner"));
    }
    private static async Task<IResult> DeleteJourneyAsync(
        string id,
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        // Find the journey.
        var journey = await db.Journeys.SingleOrDefaultAsync(
            journey => journey.Id == id,
            cancellationToken);

        if (journey is null)
            return Results.NotFound(
                new { message = "Journey not found." });

        // Only its owner can delete it.
        if (journey.OwnerId != userId)
            return Results.Json(
                new { message = "Access denied." },
                statusCode: StatusCodes.Status403Forbidden);

        // Mark it for deletion, then save the change.
        db.Journeys.Remove(journey);
        await db.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }
    private static async Task<IResult> DuplicateJourneyAsync(
        string id,
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        // Load the original journey and everything needed to copy it.
        var original = await db.Journeys
            .Include(journey => journey.Owner)
            .Include(journey => journey.Points)
            .Include(journey => journey.Segments)
            .SingleOrDefaultAsync(
                journey => journey.Id == id,
                cancellationToken);

        if (original is null)
            return Results.NotFound(
                new { message = "Journey not found." });

        // Only the owner may duplicate this journey.
        if (original.OwnerId != userId)
            return Results.Json(
                new { message = "Access denied." },
                statusCode: StatusCodes.Status403Forbidden);

        // Reuse the existing mapper to prepare the route and annotations.
        var snapshot = JourneyMapper.ToResponse(original, "owner");

        var copyRequest = new CreateJourneyRequest(
            $"{original.Name} (copy)",
            snapshot.Date,
            snapshot.Plan,
            snapshot.Annotations);

        // ToEntity constructs new model objects with a new journey ID.
        var copy = JourneyMapper.ToEntity(copyRequest, original.OwnerId);
        copy.Owner = original.Owner;

        db.Journeys.Add(copy);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/journeys/{copy.Id}",
            JourneyMapper.ToResponse(copy, "owner"));
    }
    
}
