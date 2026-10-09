namespace RouteWise.Api.Models;

public sealed class JourneySegment
{
    public string JourneyId { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public int Position { get; set; }
    public string FromPointId { get; set; } = string.Empty;
    public string ToPointId { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
    public double DurationSeconds { get; set; }
    public List<PathCoordinate> Path { get; set; } = [];
}

public sealed class PathCoordinate
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}
