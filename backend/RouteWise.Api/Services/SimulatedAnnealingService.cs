namespace RouteWise.Api.Services
{
    public class SimulatedAnnealingService : IRouteOptimiser
    {
        // calculate total cost of a route
        // costMatrix can contain distance (in metres) or duration (in seconds), depending on the user chosen objective
        private static double RouteCost(
            int[] route,
            double[,] costMatrix)
        {
            double totalCost = 0;

            for (int i = 0; i < route.Length - 1; i += 1)
            {
                int from = route[i];
                int to = route[i + 1];

                totalCost += costMatrix[from, to];
            }

            return totalCost;
        }

        // Calculate the number of iterations of the Simulated Annealing Algorithm
        private static int CalculateIterations(int numReorderableLocations)
        {
            if (numReorderableLocations < 2)
            {
                return 0;
            }

            if (numReorderableLocations == 2)
            {
                return 1;
            }

            // Calculate ln(n!) by exploiting the fact that log(a*b) = log(a) + log(b)
            // Therefore no need to multiply all the numbers to get the factorial
            // That number would be super big
            double lnFactorial = 0;

            for (int i = 2; i <= numReorderableLocations; i += 1)
            {
                lnFactorial += Math.Log(i);
            }

            // estimatedIterations = round up 5n * ln(n!)
            int estimatedIterations = (int)Math.Ceiling(5 * numReorderableLocations * lnFactorial);

            return Math.Min(2222, estimatedIterations);
        }

        // generate a neighbouring route by swapping 2 destinations
        // start location is always fixed
        // end location is fixed when fixedEnd is true (when user chooses fixed end or return to start)
        private static int[] GenerateNeighbour(
            int[] route,
            bool hasFixedEnd,
            Random random)
        {
            // if hasFixedEnd then lastSwappableIndex can point to second to last position
            // else point to last
            int lastSwappableIndex = hasFixedEnd
                ? route.Length - 2
                : route.Length - 1;

            // There must be at least 2 reorderable locations to be swapped
            // else there's no neighbour to return, so just return the current
            if (lastSwappableIndex < 2)
            {
                return route;
            }

            // copy current route
            int[] neighbour = (int[])route.Clone();

            // randomise 2 destination indices
            int first = random.Next(1, lastSwappableIndex + 1);
            int second = random.Next(1, lastSwappableIndex + 1);

            // cannot be the same index
            while (second == first)
            {
                second = random.Next(1, lastSwappableIndex + 1);
            }

            // swap
            int temp = neighbour[first];
            neighbour[first] = neighbour[second];
            neighbour[second] = temp;

            return neighbour;
        }

        // calculate initial temperature based on average segment cost of user's initial route
        // so Simulated Annealing's probability calculation based on temperature
        // adapts to different journey lengths and cost units (metres or seconds)
        private static double InitialTemperature(
            int[] route,
            double[,] costMatrix)
        {
            double totalCost = RouteCost(route, costMatrix);
            int segmentCount = route.Length - 1;

            double averageSegmentCost = totalCost / segmentCount;

            // 1.0 is to avoid temperature going too low
            return Math.Max(averageSegmentCost, 1.0);
        }

        // cooling schedule
        // T(k) = T0 * (Tf / T0)^(k / (I - 1))
        // Tf = 0.01 * T0 so the temperature decreases smoothly from T0 to 1% of T0 over the iterations
        private static double CoolingSchedule(
            int iteration,
            int totalIterations,
            double initialTemperature)
        {
            if (totalIterations <= 1)
            {
                return initialTemperature;
            }

            double progress = (double)iteration / (totalIterations - 1);

            return initialTemperature * Math.Pow(0.01, progress);
        }

        // validate input order, cost matrix, and ending modes
        private static void ValidateInput(
            double[,] costMatrix,
            int[] enteredOrder,
            bool fixedEnd,
            bool returnToStart)
        {
            ArgumentNullException.ThrowIfNull(costMatrix);
            ArgumentNullException.ThrowIfNull(enteredOrder);

            int locationCount = costMatrix.GetLength(0);

            // cost matrix must be square
            if (locationCount < 2 || costMatrix.GetLength(1) != locationCount)
            {
                throw new ArgumentException("Cost matrix must be square and contain at least 2 locations.");
            }

            // enteredOrder must include every location exactly once
            if (enteredOrder.Length != locationCount)
            {
                throw new ArgumentException("Entered order must contain every location.");
            }

            bool[] visited = new bool[locationCount];

            foreach(int index in enteredOrder)
            {
                if (index < 0 || index >= locationCount || visited[index])
                {
                    throw new ArgumentException("Entered order contains an invalid or duplicate index.");
                }

                visited[index] = true;
            }

            // can only have 1 ending mode
            if (fixedEnd && returnToStart)
            {
                throw new ArgumentException("Fixed end and return-to-start ending modes cannot both be selected.");
            }

            // travel costs must be valid
            for (int i = 0; i < locationCount; i += 1)
            {
                for (int j = 0; j < locationCount; j += 1)
                {
                    double cost = costMatrix[i, j];

                    if (!double.IsFinite(cost) || cost < 0)
                    {
                        throw new ArgumentException("Travel costs must be finite and n on-negative.");
                    }
                } 
            }
        }
        public int[] OptimiseRoute(
            double[,] costMatrix,
            int[] enteredOrder,
            bool fixedEnd = false, bool returnToStart = false,
            int? randomSeed = null)
        {
            // validate input
            ValidateInput(costMatrix, enteredOrder, fixedEnd, returnToStart);

            Random random;

            if (randomSeed.HasValue)
            {
                random = new Random(randomSeed.Value);
            }
            else
            {
                random = new Random();
            }

            bool hasFixedEnd = fixedEnd || returnToStart;

            // start from user's entered location order
            int[] currentRoute = (int[])enteredOrder.Clone();

            // if returnToStart then append the starting location
            if (returnToStart)
            {
                // have to do all this because arrays in C# are not resizable
                int[] returnRoute = new int[currentRoute.Length + 1];

                Array.Copy(currentRoute, returnRoute, currentRoute.Length);
                returnRoute[^1] = currentRoute[0]; // append start location in currentRoute to last position in returnRoute

                currentRoute = returnRoute;
            }

            double currentCost = RouteCost(currentRoute, costMatrix);

            // store best solution found so far
            // currentRoute is stored as the initial best
            int[] bestRoute = (int[])currentRoute.Clone();
            double bestCost = currentCost;

            // reorderable locations count
            int numReorderableLocations = hasFixedEnd
                ? currentRoute.Length - 2
                : currentRoute.Length - 1;

            // fewer than 2 destinations then no optimisation needed
            if (numReorderableLocations < 2)
            {
                return bestRoute;
            }

            // 2 destinations then compare 2 possible orders
            // instead of having a bunch of iterations to randomise neighbours
            if (numReorderableLocations == 2)
            {
                int[] alternative = (int[])currentRoute.Clone();

                int temp = alternative[1];
                alternative[1] = alternative[2];
                alternative[2] = temp;

                double alternativeCost = RouteCost(alternative, costMatrix);

                if (alternativeCost < bestCost)
                {
                    return alternative;
                }

                return bestRoute;
            }

            // more than 2 destinations then use normal Simulated Annealing process
            int iterations = CalculateIterations(numReorderableLocations);

            double initialTemperature = InitialTemperature(currentRoute, costMatrix);

            for (int iteration = 0; iteration < iterations; iteration += 1)
            {
                // cool
                // at iteration 0 then the temperature is kept the same as initialTemperature
                double temperature = CoolingSchedule(iteration, iterations, initialTemperature);

                // generate neighbouring route and calculate neighbour cost
                int[] neighbourRoute = GenerateNeighbour(currentRoute, hasFixedEnd, random);
                double neighbourCost = RouteCost(neighbourRoute, costMatrix);

                double costChange = neighbourCost - currentCost;

                bool acceptNeighbour = false;

                // always accept better or equally good route
                if (costChange <= 0)
                {
                    acceptNeighbour = true;
                }
                // might accept worse route
                else
                {
                    // calculate probability based on temperature (T) and cost change (delta)
                    double acceptanceProbability = Math.Exp(-costChange / temperature);
                    double randomValue = random.NextDouble();
                    if (randomValue < acceptanceProbability)
                    {
                        acceptNeighbour = true;
                    }
                }

                // update current route if accept neighbour
                if (acceptNeighbour)
                {
                    currentRoute = neighbourRoute;
                    currentCost = neighbourCost;
                }

                // update best route if cheaper
                if (currentCost < bestCost)
                {
                    bestRoute = (int[])currentRoute.Clone();
                    bestCost = currentCost;
                }
            }

            return bestRoute;
        }
    }
}
