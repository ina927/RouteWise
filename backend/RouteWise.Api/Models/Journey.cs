namespace RouteWise.Api.Models;

public sealed class Journey
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string OwnerId { get; set; } = string.Empty;
    public User Owner { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
    public TravelMode TravelMode { get; set; }
    public OptimisationObjective Objective { get; set; }
    public EndMode EndMode { get; set; }
    public List<JourneyPoint> Points { get; set; } = [];
    public List<JourneySegment> Segments { get; set; } = [];
}
