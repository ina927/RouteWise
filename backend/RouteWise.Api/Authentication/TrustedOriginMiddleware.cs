namespace RouteWise.Api.Authentication;

// Browser Origin cannot be set by page JavaScript. Fail closed for missing/null
// origins as well; non-browser API clients must supply the trusted origin explicitly.
public sealed class TrustedOriginMiddleware
{
    private readonly RequestDelegate next;
    private readonly HashSet<string> allowedOrigins;

    public TrustedOriginMiddleware(RequestDelegate next, IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        this.next = next;
        var configured = configuration.GetSection("Authentication:AllowedOrigins").Get<string[]>();
        allowedOrigins = new HashSet<string>(configured ?? (environment.IsDevelopment()
            ? ["http://localhost:5173", "http://localhost:5000"]
            : []), StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var changesState = !HttpMethods.IsGet(request.Method)
            && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method);

        if (request.Path.StartsWithSegments("/api") && changesState)
        {
            var origins = request.Headers.Origin;
            if (origins.Count != 1 || !allowedOrigins.Contains(origins[0] ?? string.Empty))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Request origin is not allowed." });
                return;
            }
        }

        await next(context);
    }
}
