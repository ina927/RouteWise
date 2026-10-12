namespace RouteWise.Api.Contracts.Auth;

public sealed record CurrentUserResponse(
    string Id,
    string Username);