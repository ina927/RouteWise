namespace RouteWise.Api.Contracts.Auth;

public sealed record SignupRequest(
    string? Username,
    string? Password);