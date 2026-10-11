using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Data;
using RouteWise.Api.Mapping;
using RouteWise.Api.Models;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Validation;

namespace RouteWise.Api.Endpoints;

public static class JourneyEndpoints
{
    public static void MapJourneyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/journeys", GetOwnedJourneysAsync).RequireAuthorization();
        app.MapGet("/api/journeys/{id}", GetJourneyAsync).RequireAuthorization();

        app.MapPost("/api/journeys", CreateJourneyAsync).RequireAuthorization();
        app.MapPatch("/api/journeys/{id}", RenameJourneyAsync).RequireAuthorization();
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

    private static async Task<IResult> GetJourneyAsync(string id, HttpContext context,
        RouteWiseDbContext db, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var journey = await db.Journeys.AsNoTracking()
            .Include(journey => journey.Owner)
            .Include(journey => journey.Points)
            .Include(journey => journey.Segments)
            .SingleOrDefaultAsync(journey => journey.Id == id, cancellationToken);

        if (journey is null)
            return Results.NotFound(new { message = "Journey not found." });

        var isOwner = journey.OwnerId == userId;
        if (!isOwner)
        {
            var hasSharedAccess = await db.Invitations.AnyAsync(invitation =>
                invitation.JourneyId == id && invitation.RecipientId == userId &&
                invitation.Status == InvitationStatus.Accepted && !invitation.RemovedFromShared,
                cancellationToken);
            if (!hasSharedAccess)
                return Results.Json(new { message = "Access denied." },
                    statusCode: StatusCodes.Status403Forbidden);
        }

        return Results.Ok(JourneyMapper.ToResponse(journey, isOwner ? "owner" : "shared"));
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
    private static async Task<IResult> RenameJourneyAsync(
        string id,
        RenameJourneyRequest request,
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

        // Validate before changing anything.
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(
                new { message = "Journey name is required." });

        journey.Name = request.Name.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(
            JourneyMapper.ToResponse(journey, "owner"));
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
