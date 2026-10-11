using System.Text.Json.Serialization;

namespace RouteWise.Api.Contracts.Journeys;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RenameJourneyRequest(string? Name);