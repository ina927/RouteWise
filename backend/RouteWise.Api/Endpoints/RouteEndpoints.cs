using RouteWise.Api.Contracts.Routes;
using RouteWise.Api.Services;

namespace RouteWise.Api.Endpoints;

public static class RouteEndpoints
{
    public static void MapRouteEndpoints(this WebApplication app)
    {
        // optimise the order of locations
        app.MapPost("/api/routes/optimize", OptimiseRouteAsync)
            .RequireAuthorization();

        // calculate a route in the given order
        app.MapPost("/api/routes/calculate", CalculateRouteAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> OptimiseRouteAsync(
        OptimizeRouteRequest request,
        RoutePlanningService service,
        CancellationToken cancellationToken)
    {
        try
        {
            // get optimised route
            var result = await service.OptimiseAsync(
                request,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            // return validation error
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> CalculateRouteAsync(
        CalculateRouteRequest request,
        RoutePlanningService service,
        CancellationToken cancellationToken)
    {
        try
        {
            // calculate route without optimisation
            var result = await service.CalculateAsync(
                request,
                cancellationToken);

            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            // return validation error
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
