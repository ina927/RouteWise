using NUnit.Framework;
using RouteWise.Api.Services;

namespace RouteWise.Tests
{
    public class SimulatedAnnealingServiceTests
    {
        private static SimulatedAnnealingService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _service = new SimulatedAnnealingService();
        }

        // Test methods

        // basic routes
        // 1 destination
        // 2 destinations

        // route modes
        // open route
        // fixed end
        // return to start

        // route integrity
        // allocation preserved
        // original route unchanged

        // optimisation
        // cost improvement
        // known optimal route

        // randomness
        // random seed

        // input validation
        // invalid cost matrix
        // invalid route
        // invalid end mode
    }
}
