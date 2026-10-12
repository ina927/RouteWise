using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Contracts.Invitations;
using RouteWise.Api.Data;
using RouteWise.Api.Mapping;
using RouteWise.Api.Models;

namespace RouteWise.Api.Endpoints;

public static class InvitationEndpoints
{
    public static void MapInvitationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/invitations", GetIncomingInvitationsAsync)
            .RequireAuthorization();
        app.MapPost("/api/journeys/{id}/invitations", SendInvitationAsync)
            .RequireAuthorization();
        app.MapPatch("/api/invitations/{id}", RespondToInvitationAsync)
            .RequireAuthorization();
        app.MapGet("/api/shared-journeys", GetSharedJourneysAsync)
            .RequireAuthorization();
        app.MapDelete("/api/shared-journeys/{id}", RemoveSharedJourneyAsync)
            .RequireAuthorization();
        app.MapGet("/api/journeys/{id}/invitations",GetJourneyInvitationsAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetIncomingInvitationsAsync(
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var invitations = await db.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.RecipientId == userId)
            .Include(invitation => invitation.Sender)
            .Include(invitation => invitation.Recipient)
            .Include(invitation => invitation.Journey)
                .ThenInclude(journey => journey.Owner)
            .Include(invitation => invitation.Journey)
                .ThenInclude(journey => journey.Points)
            .Include(invitation => invitation.Journey)
                .ThenInclude(journey => journey.Segments)
            .ToListAsync(cancellationToken);

        var responses = invitations
            .Select(invitation => InvitationMapper.ToResponse(invitation))
            .ToList();

        return Results.Ok(responses);
    }

    private static async Task<IResult> SendInvitationAsync(
        string id,
        SendInvitationRequest request,
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        var journey = await db.Journeys
            .Include(journey => journey.Owner)
            .Include(journey => journey.Points)
            .Include(journey => journey.Segments)
            .SingleOrDefaultAsync(journey => journey.Id == id, cancellationToken);

        if (journey is null)
            return Results.NotFound(new { message = "Journey not found." });

        if (journey.OwnerId != userId)
            return Results.Json(new { message = "Access denied." },
                statusCode: StatusCodes.Status403Forbidden);

        if (string.IsNullOrWhiteSpace(request.RecipientId))
            return Results.BadRequest(new { message = "Recipient is required." });

        if (request.RecipientId == userId)
            return Results.BadRequest(new { message = "Choose another user to invite." });

        var recipient = await db.Users.SingleOrDefaultAsync(
            user => user.Id == request.RecipientId, cancellationToken);
        if (recipient is null)
            return Results.NotFound(new { message = "User not found." });

        // Provisional mock-compatible policy; reinvites remain a product decision.
        // This lookup alone does not guarantee uniqueness under concurrent requests.
        var alreadyInvited = await db.Invitations.AnyAsync(invitation =>
            invitation.JourneyId == id && invitation.RecipientId == recipient.Id,
            cancellationToken);
        if (alreadyInvited)
            return Results.Conflict(new { message = "An invitation already exists for this user and journey." });

        var invitation = new Invitation
        {
            JourneyId = journey.Id,
            Journey = journey,
            SenderId = journey.OwnerId,
            Sender = journey.Owner,
            RecipientId = recipient.Id,
            Recipient = recipient,
            Status = InvitationStatus.Pending
        };

        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Json(InvitationMapper.ToResponse(invitation),
            statusCode: StatusCodes.Status201Created);
    }
    private static async Task<IResult> RespondToInvitationAsync(
        string id,
        RespondToInvitationRequest request,
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        // Load the invitation and everything the response mapper needs.
        var invitation = await db.Invitations
            .AsNoTracking()
            .Include(invitation => invitation.Sender)
            .Include(invitation => invitation.Recipient)
            .Include(invitation => invitation.Journey)
                .ThenInclude(journey => journey.Owner)
            .Include(invitation => invitation.Journey)
                .ThenInclude(journey => journey.Points)
            .Include(invitation => invitation.Journey)
                .ThenInclude(journey => journey.Segments)
            .SingleOrDefaultAsync(
                invitation => invitation.Id == id,
                cancellationToken);

        if (invitation is null)
            return Results.NotFound(
                new { message = "Invitation not found." });

        // Only the invited person can respond.
        if (invitation.RecipientId != userId)
            return Results.Json(
                new { message = "Only the recipient can respond." },
                statusCode: StatusCodes.Status403Forbidden);

        if (request.Status is not ("accepted" or "declined"))
            return Results.BadRequest(
                new { message = "Status must be accepted or declined." });

        var newStatus = request.Status == "accepted"
            ? InvitationStatus.Accepted
            : InvitationStatus.Declined;

        // Update only if this invitation is still pending.
        var updatedCount = await db.Invitations
            .Where(item =>
                item.Id == id &&
                item.RecipientId == userId &&
                item.Status == InvitationStatus.Pending)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    item => item.Status,
                    newStatus),
                cancellationToken);

        if (updatedCount == 0)
            return Results.Conflict(
                new { message = "This invitation is no longer pending." });

        // Update the in-memory object used to build the response.
        invitation.Status = newStatus;

        return Results.Ok(
            InvitationMapper.ToResponse(invitation));
    }
    private static async Task<IResult> GetSharedJourneysAsync(
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var journeys = await db.Journeys
            .AsNoTracking()
            .Where(journey => db.Invitations.Any(invitation =>
                invitation.JourneyId == journey.Id &&
                invitation.RecipientId == userId &&
                invitation.Status == InvitationStatus.Accepted &&
                !invitation.RemovedFromShared))
            .Include(journey => journey.Owner)
            .Include(journey => journey.Points)
            .Include(journey => journey.Segments)
            .ToListAsync(cancellationToken);

        var summaries = journeys.Select(journey =>
        {
            var representativePlaceId = journey.Points
                .OrderBy(point => point.Position)
                .First()
                .PlaceId;

            return JourneyMapper.ToSummary(
                journey,
                representativePlaceId);
        }).ToList();

        return Results.Ok(summaries);
    }
    private static async Task<IResult> RemoveSharedJourneyAsync(
        string id,
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        var updatedCount = await db.Invitations
            .Where(invitation =>
                invitation.JourneyId == id &&
                invitation.RecipientId == userId &&
                invitation.Status == InvitationStatus.Accepted &&
                !invitation.RemovedFromShared)
            .ExecuteUpdateAsync(
                updates => updates.SetProperty(
                    invitation => invitation.RemovedFromShared,
                    true),
                cancellationToken);

        if (updatedCount == 0)
            return Results.NotFound(
                new { message = "Accepted shared journey not found." });

        return Results.NoContent();
    }
    private static async Task<IResult> GetJourneyInvitationsAsync(
        string id,
        HttpContext context,
        RouteWiseDbContext db,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        // First check that the journey exists and belongs to this user.
        var journey = await db.Journeys
            .AsNoTracking()
            .SingleOrDefaultAsync(
                journey => journey.Id == id,
                cancellationToken);

        if (journey is null)
            return Results.NotFound(
                new { message = "Journey not found." });

        if (journey.OwnerId != userId)
            return Results.Json(
                new { message = "Only the owner can view these invitations." },
                statusCode: StatusCodes.Status403Forbidden);

        // Load this journey's invitations and their response data.
        var invitations = await db.Invitations
            .AsNoTracking()
            .Where(invitation => invitation.JourneyId == id)
            .Include(invitation => invitation.Sender)
            .Include(invitation => invitation.Recipient)
            .Include(invitation => invitation.Journey)
                .ThenInclude(journey => journey.Owner)
            .Include(invitation => invitation.Journey)
                .ThenInclude(journey => journey.Points)
            .Include(invitation => invitation.Journey)
                .ThenInclude(journey => journey.Segments)
            .ToListAsync(cancellationToken);

        var responses = invitations
            .Select(invitation => InvitationMapper.ToResponse(invitation))
            .ToList();

        return Results.Ok(responses);
    }

}
