using RouteWise.Api.Contracts.Journeys;

namespace RouteWise.Api.Services
{
    // Defines the operations that any routing provider must support
    // GoogleRoutesService will implement this interface.
    public interface IRoutingService
    {
        // Gets the travel cost between every pair of points
        // The returned matrix is used by SimulatedAnnealingService to find an efficient order in which to visit destinations
        Task<double[,]> GetCostMatrixAsync(
            IReadOnlyList<RoutePointDto> points,
            string travelMode, string objective,
            CancellationToken cancellationToken = default
        );

        // Calculates the actual route through points in the supplied order
        // returns the complete route unlike GetCostmatrixAsync
        // including segments, distances, durations, and coordinates
        // the frontend can then display on a map
        Task<RouteResultDto> CalculateRouteAsync(
            IReadOnlyList<RoutePointDto> points,
            string travelMode,
            CancellationToken cancellationToken = default
        );
    }
}