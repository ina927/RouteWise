namespace RouteWise.Api.Models;

public sealed class Invitation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string JourneyId { get; set; } = string.Empty;
    public Journey Journey { get; set; } = null!;
    public string SenderId { get; set; } = string.Empty;
    public User Sender { get; set; } = null!;
    public string RecipientId { get; set; } = string.Empty;
    public User Recipient { get; set; } = null!;
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    // Proposed storage for removal without introducing a fourth status.
    public bool RemovedFromShared { get; set; }
}
