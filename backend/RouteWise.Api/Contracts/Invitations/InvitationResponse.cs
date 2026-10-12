using RouteWise.Api.Contracts.Journeys;
using RouteWise.Api.Contracts.Users;

namespace RouteWise.Api.Contracts.Invitations;

public sealed record InvitationResponse(
    string Id,
    JourneySummaryResponse Journey,
    UserSummaryResponse Sender,
    UserSummaryResponse Recipient,
    string Status);