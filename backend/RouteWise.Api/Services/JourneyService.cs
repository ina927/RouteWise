using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Data;
using RouteWise.Api.Mapping;
using RouteWise.Api.Models;

namespace RouteWise.Api.Services;

public sealed class JourneyService(RouteWiseDbContext db)
{
    public async Task<ServiceResult<JourneyResponse>> GetAsync(
        string id,
        string userId,
        CancellationToken cancellationToken)
    {
        var journey = await db.Journeys
            .AsNoTracking()
            .Include(journey => journey.Owner)
            .Include(journey => journey.Points)
            .Include(journey => journey.Segments)
            .SingleOrDefaultAsync(journey => journey.Id == id, cancellationToken);

        if (journey is null)
            return ServiceResult<JourneyResponse>.Failure(
                ServiceError.NotFound, "Journey not found.");

        var isOwner = journey.OwnerId == userId;
        if (!isOwner)
        {
            var hasSharedAccess = await db.Invitations.AnyAsync(invitation =>
                invitation.JourneyId == id && invitation.RecipientId == userId &&
                invitation.Status == InvitationStatus.Accepted && !invitation.RemovedFromShared,
                cancellationToken);

            if (!hasSharedAccess)
                return ServiceResult<JourneyResponse>.Failure(
                    ServiceError.Forbidden, "Access denied.");
        }

        return ServiceResult<JourneyResponse>.Success(
            JourneyMapper.ToResponse(journey, isOwner ? "owner" : "shared"));
    }
}
