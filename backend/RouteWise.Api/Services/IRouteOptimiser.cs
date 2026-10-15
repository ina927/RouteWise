namespace RouteWise.Api.Services
{
    // Defines a contract for any route optimisation algorithm
    // Simulated Annealing implements this interface for this project
    // In the future, another algorithm (e.g. Genetic Algorithm) could implement the same interface without changing the code that calls the optimiser.
    public interface IRouteOptimiser
    {
        // Returns the optimised order of point indices
        int[] OptimiseRoute(
            double[,] costMatrix,
            int[] enteredOrder,
            bool fixedEnd = false,
            bool returnToStart = false,
            int? randomSeed = null
        );
    }
}