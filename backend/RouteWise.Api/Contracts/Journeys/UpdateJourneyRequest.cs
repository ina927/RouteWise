using System.Text.Json;
using System.Text.Json.Serialization;

namespace RouteWise.Api.Contracts.Journeys;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateJourneyRequest
{
    // Undefined means omitted; Null means explicitly supplied as null.
    public JsonElement Name { get; init; }

    public JsonElement Date { get; init; }

    public JsonElement Annotations { get; init; }

    public JsonElement Plan { get; init; }
}
