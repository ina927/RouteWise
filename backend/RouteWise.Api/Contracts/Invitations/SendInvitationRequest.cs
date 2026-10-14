namespace RouteWise.Api.Contracts.Invitations;

public sealed record SendInvitationRequest(
    string? RecipientId);