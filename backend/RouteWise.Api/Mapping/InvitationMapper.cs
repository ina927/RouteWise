using RouteWise.Api.Contracts.Invitations;
using RouteWise.Api.Contracts.Users;
using RouteWise.Api.Models;

namespace RouteWise.Api.Mapping;

public static class InvitationMapper
{
    // The caller must load Journey.Owner, Journey.Points,
    // Journey.Segments, Sender, and Recipient before mapping.
    public static InvitationResponse ToResponse(Invitation invitation)
    {
        var journey = invitation.Journey;

        // Use the same first-stop photo choice as the journey list.
        var representativePlaceId = journey.Points
            .OrderBy(point => point.Position)
            .First()
            .PlaceId;

        var journeySummary = JourneyMapper.ToSummary(
            journey,
            representativePlaceId);

        var sender = new UserSummaryResponse(
            invitation.Sender.Id,
            invitation.Sender.Username);

        var recipient = new UserSummaryResponse(
            invitation.Recipient.Id,
            invitation.Recipient.Username);

        var status = invitation.Status switch
        {
            InvitationStatus.Pending => "pending",
            InvitationStatus.Accepted => "accepted",
            InvitationStatus.Declined => "declined",
            _ => throw new InvalidOperationException(
                "Unsupported invitation status.")
        };

        return new InvitationResponse(
            invitation.Id,
            journeySummary,
            sender,
            recipient,
            status);
    }
}