using RouteWise.Api.Contracts.Journeys;

namespace RouteWise.Api.Services;

// Defines the operations available for managing journeys
public interface IJourneyService
{
    // Retrieves a journey that the specified user is allowed to access
    // Returns a ServiceResult because the operation may 
        // succeed,
        // fail because the journey does not exist, 
        // or fail because the user does not have permission to access it.
    Task<ServiceResult<JourneyResponse>> GetAsync(
        string id,
        string userId,
        CancellationToken cancellationToken
    );
}