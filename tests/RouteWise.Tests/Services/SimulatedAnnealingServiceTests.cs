
using NUnit.Framework;
using RouteWise.Api.Services;

namespace RouteWise.Tests.Services
{
    [TestFixture]
    public class SimulatedAnnealingServiceTests
    {
        private static SimulatedAnnealingService _service = null!;

        [SetUp]
        public void SetUp()
        {
            _service = new SimulatedAnnealingService();
        }

        // Helper method to calculate the total cost of a route
        // We use this to compare the original route with the optimised route
        private static double CalculateCost(int[] route, double[,] matrix)
        {
            double total = 0;

            for (int i = 0; i < route.Length - 1; i++)
            {
                total += matrix[route[i], route[i + 1]];
            }

            return total;
        }

        // Helper method to create a cost matrix
        // The diagonal is 0 because travelling to the same location costs 0
        private static double[,] CreateMatrix(int size)
        {
            var matrix = new double[size, size];

            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    matrix[i, j] = i == j
                        ? 0
                        : Math.Abs(i - j) * 10;
                }
            }

            return matrix;
        }

        // Test methods

        // basic routes
        // 1 destination
        [Test]
        public void OptimiseRoute_OneDestination_ReturnsOriginalRoute()
        {
            double[,] matrix =
            {
                { 0, 10 },
                { 10, 0 }
            };

            int[] enteredOrder = { 0, 1 };

            int[] result = _service.OptimiseRoute(matrix, enteredOrder);

            // Only one destination exists, so there is nothing to reorder.
            Assert.That(result, Is.EqualTo(new[] { 0, 1 }));
        }

        // 2 destinations
        [Test]
        public void OptimiseRoute_TwoDestinations_ChoosesCheaperOrder()
        {
            double[,] matrix =
            {
                { 0, 10, 20 },
                { 10, 0, 100 },
                { 10, 5, 0 }
            };

            int[] enteredOrder = { 0, 1, 2 };

            // Original: 0 -> 1 -> 2 = 10 + 100 = 110
            // Alternative: 0 -> 2 -> 1 = 20 + 5 = 25
            int[] result = _service.OptimiseRoute(matrix, enteredOrder);

            Assert.That(result, Is.EqualTo(new[] { 0, 2, 1 }));
            Assert.That(CalculateCost(result, matrix), Is.EqualTo(25));
        }

        // route modes
        // open route
        [Test]
        public void OptimiseRoute_OpenRoute_KeepsStartFixed()
        {
            double[,] matrix = CreateMatrix(5);
            int[] enteredOrder = { 0, 4, 2, 1, 3 };

            int[] result = _service.OptimiseRoute(
                matrix,
                enteredOrder,
                randomSeed: 42);

            // The first location must always remain the start.
            Assert.That(result[0], Is.EqualTo(0));

            // Open route: every location appears exactly once.
            Assert.That(result.OrderBy(x => x),
                Is.EqualTo(enteredOrder.OrderBy(x => x)));
        }

        // fixed end
        [Test]
        public void OptimiseRoute_FixedEnd_KeepsBothEndpointsFixed()
        {
            double[,] matrix = CreateMatrix(5);
            int[] enteredOrder = { 0, 3, 1, 2, 4 };

            int[] result = _service.OptimiseRoute(
                matrix,
                enteredOrder,
                fixedEnd: true,
                randomSeed: 42);

            Assert.Multiple(() =>
            {
                Assert.That(result[0], Is.EqualTo(0));
                Assert.That(result[^1], Is.EqualTo(4));
                Assert.That(result.Length, Is.EqualTo(5));
            });
        }

        // return to start
        [Test]
        public void OptimiseRoute_ReturnToStart_AppendsStartingLocation()
        {
            double[,] matrix = CreateMatrix(4);
            int[] enteredOrder = { 0, 3, 1, 2 };

            int[] result = _service.OptimiseRoute(
                matrix,
                enteredOrder,
                returnToStart: true,
                randomSeed: 42);

            // The starting location should appear again at the end.
            Assert.Multiple(() =>
            {
                Assert.That(result[0], Is.EqualTo(0));
                Assert.That(result[^1], Is.EqualTo(0));
                Assert.That(result.Length, Is.EqualTo(5));
            });

            // Apart from the final return, each location is visited once.
            Assert.That(result.Take(result.Length - 1).OrderBy(x => x),
                Is.EqualTo(enteredOrder.OrderBy(x => x)));
        }

        // route integrity
        // allocation preserved
        [Test]
        public void OptimiseRoute_AllLocationsPreserved()
        {
            double[,] matrix = CreateMatrix(6);
            int[] enteredOrder = { 0, 5, 3, 1, 4, 2 };

            int[] result = _service.OptimiseRoute(
                matrix,
                enteredOrder,
                randomSeed: 123);

            Assert.That(result.Length, Is.EqualTo(enteredOrder.Length));

            // Compare sorted indices to verify no locations were
            // lost, duplicated, or replaced.
            Assert.That(result.OrderBy(x => x),
                Is.EqualTo(enteredOrder.OrderBy(x => x)));
        }

        // original route unchanged
        [Test]
        public void OptimiseRoute_DoesNotModifyOriginalRoute()
        {
            double[,] matrix = CreateMatrix(4);
            int[] enteredOrder = { 0, 3, 2, 1 };
            int[] originalCopy = (int[])enteredOrder.Clone();

            _service.OptimiseRoute(
                matrix,
                enteredOrder,
                randomSeed: 42);

            Assert.That(enteredOrder, Is.EqualTo(originalCopy));
        }

        // optimisation
        // cost improvement
        [Test]
        public void OptimiseRoute_NeverReturnsMoreExpensiveRoute()
        {
            double[,] matrix = CreateMatrix(6);
            int[] enteredOrder = { 0, 5, 1, 4, 2, 3 };

            double originalCost = CalculateCost(enteredOrder, matrix);

            int[] result = _service.OptimiseRoute(
                matrix,
                enteredOrder,
                randomSeed: 42);

            double optimisedCost = CalculateCost(result, matrix);

            // Simulated Annealing may temporarily accept worse routes,
            // but the service stores and returns the best route found.
            Assert.That(optimisedCost,
                Is.LessThanOrEqualTo(originalCost));
        }

        // known optimal route
        [Test]
        public void OptimiseRoute_KnownOptimalRoute_ReturnsExpectedOrder()
        {
            double[,] matrix =
            {
                { 0, 10, 20 },
                { 10, 0, 100 },
                { 10, 5, 0 }
            };

            int[] enteredOrder = { 0, 1, 2 };

            // There are exactly two possible destination orders.
            // The implementation compares both directly.
            int[] result = _service.OptimiseRoute(
                matrix,
                enteredOrder);

            Assert.That(result, Is.EqualTo(new[] { 0, 2, 1 }));
        }

        // randomness
        // random seed
        [Test]
        public void OptimiseRoute_SameSeed_ReturnsSameRoute()
        {
            double[,] matrix = CreateMatrix(7);
            int[] enteredOrder = { 0, 6, 2, 5, 1, 4, 3 };

            int[] firstResult = _service.OptimiseRoute(
                matrix,
                enteredOrder,
                randomSeed: 12345);

            int[] secondResult = _service.OptimiseRoute(
                matrix,
                enteredOrder,
                randomSeed: 12345);

            Assert.That(firstResult, Is.EqualTo(secondResult));
        }

        // input validation
        // invalid cost matrix
        [Test]
        public void OptimiseRoute_NonSquareMatrix_Throws()
        {
            double[,] matrix = new double[2, 3];
            int[] enteredOrder = { 0, 1 };

            Assert.Throws<ArgumentException>(() =>
                _service.OptimiseRoute(matrix, enteredOrder));
        }

        [Test]
        public void OptimiseRoute_NegativeCost_Throws()
        {
            double[,] matrix =
            {
                { 0, -10 },
                { 10, 0 }
            };

            Assert.Throws<ArgumentException>(() =>
                _service.OptimiseRoute(matrix, new[] { 0, 1 }));
        }

        [Test]
        public void OptimiseRoute_NonFiniteCost_Throws()
        {
            double[,] matrix =
            {
                { 0, double.NaN },
                { 10, 0 }
            };

            Assert.Throws<ArgumentException>(() =>
                _service.OptimiseRoute(matrix, new[] { 0, 1 }));
        }

        // invalid route
        [Test]
        public void OptimiseRoute_DuplicateLocation_Throws()
        {
            double[,] matrix = CreateMatrix(3);
            int[] enteredOrder = { 0, 1, 1 };

            Assert.Throws<ArgumentException>(() =>
                _service.OptimiseRoute(matrix, enteredOrder));
        }

        [Test]
        public void OptimiseRoute_MissingLocation_Throws()
        {
            double[,] matrix = CreateMatrix(3);
            int[] enteredOrder = { 0, 1 };

            Assert.Throws<ArgumentException>(() =>
                _service.OptimiseRoute(matrix, enteredOrder));
        }

        [Test]
        public void OptimiseRoute_OutOfRangeLocation_Throws()
        {
            double[,] matrix = CreateMatrix(3);
            int[] enteredOrder = { 0, 1, 5 };

            Assert.Throws<ArgumentException>(() =>
                _service.OptimiseRoute(matrix, enteredOrder));
        }

        // invalid end mode
        [Test]
        public void OptimiseRoute_ConflictingEndModes_Throws()
        {
            double[,] matrix = CreateMatrix(3);
            int[] enteredOrder = { 0, 1, 2 };

            Assert.Throws<ArgumentException>(() =>
                _service.OptimiseRoute(
                    matrix,
                    enteredOrder,
                    fixedEnd: true,
                    returnToStart: true));
        }
    }
}
