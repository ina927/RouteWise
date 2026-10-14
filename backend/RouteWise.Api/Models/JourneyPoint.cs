namespace RouteWise.Api.Models;

public sealed class JourneyPoint
{
    public string JourneyId { get; set; } = string.Empty;
    // Occurrence ID supplied by the route, scoped to this journey.
    public string Id { get; set; } = string.Empty;
    public int Position { get; set; }
    public string PlaceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Note { get; set; }
    public TimeOnly? PlannedTime { get; set; }
}
