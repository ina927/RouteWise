using Microsoft.EntityFrameworkCore;
using RouteWise.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

builder.Services.AddDbContext<RouteWiseDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("RouteWise")
        ?? "Data Source=routewise.db"));

var app = builder.Build();

app.MapHealthChecks("/api/health");

app.Run();
