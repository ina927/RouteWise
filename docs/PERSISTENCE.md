# Persistence foundation (Nam)

This is the implemented internal EF Core/SQLite model based on PROJECT_SCOPE.md and
API_CONTRACTS.md. Implementation does not establish team approval of the contracts.
The initial migration creates the schema. Accounts, journeys and sharing use this database; see
[Authentication](AUTHENTICATION.md) for the implemented boundary and remaining work.

## Local database setup

From the repository root, restore the pinned EF Core 9.0.20 local tool, then apply
the checked-in migration from the API directory:

```sh
dotnet tool restore
cd backend/RouteWise.Api
dotnet ef database update
dotnet ef migrations list
```

With the default connection string, this creates `backend/RouteWise.Api/routewise.db`.
Run the API from this directory (`dotnet run --launch-profile http`) to use the same
relative database path. A configured `ConnectionStrings:RouteWise` overrides the
default. The database is ignored by Git; commit the tool manifest and all migration
source/snapshot files so each developer can recreate it. Do not regenerate
InitialCreate after pulling it; apply the existing migration with database update.

## Model decisions

- User owns Journeys. Journey owns ordered Points and Segments.
- Point and segment IDs are scoped to a journey using composite primary keys.
  Duplicates can retain those IDs. Repeated Google place IDs are not unique.
- Position is internal storage for array order; API responses must sort by it.
  Contiguous, unique positions and the 10-location limit require service validation.
- Notes and manually entered TimeOnly values belong to point occurrences. DateOnly
  belongs to the journey. Neither affects route optimisation.
- Segment paths are typed coordinate lists stored as JSON. Segment endpoint foreign
  keys refer to points in the same journey. When replacing a route, remove old
  segments before old points, inside a transaction.
- Totals are derived from segment sums; no duplicated total fields are stored.
- Invitation references the journey, sender and recipient. RemovedFromShared is a
  implemented storage choice supporting retained history without a fourth status.
  Access must require Accepted AND !RemovedFromShared. Removal/history and reinvite
  policy still need team agreement; no unique invitation-pair rule is imposed yet.
- Database cascades delete owned journeys, their points/segments and invitations,
  plus invitations where a deleted user is sender or recipient. Other owners'
  journeys and other recipients survive.

Entities are not API response DTOs. Existing mappers return the camelCase contracts
and enum strings, reconstruct annotations and calculate summaries; journey lookup
determines access before mapping. Credentials are not exposed in responses.
Representative photo selection remains a team decision; summaries currently use
the first stop's place ID.

Signup implements username validation/normalization, password hashing and a
server-managed cookie session. Journey/sharing operations implement input validation
and ownership/access checks (including sender = journey owner). Route provenance
validation still needs agreement with Zoe: structural consistency does not prove
that client route costs are authentic road costs.

## Service extraction: first slice

`Services/JourneyService.cs` implements journey detail lookup. It receives the EF
context through dependency injection, loads the journey, and enforces owner or
accepted/non-removed recipient access. The endpoint supplies the authenticated user
ID and cancellation token; the service does not depend on `HttpContext`.

`ServiceResult<T>` describes either successful data or an expected failure with a
`ServiceError` and message. Lookup currently returns success, `NotFound`, or
`Forbidden`. The endpoint maps those outcomes to HTTP 200, 404, or 403 and returns
only the existing response DTO or `{ message }`, not the result wrapper. Unexpected
exceptions still reach the application's exception handler.

`JourneyService` is registered with `AddScoped`, matching the database context's
request lifetime. Direct database injection in other endpoints remains valid;
this extraction separates HTTP handling from the journey operation's rules.

The brief's requirement is **partially implemented**: only journey detail lookup
uses the pattern so far. Next, move creation/update into `JourneyService` while
preserving validation and transactions, then use the same result type in an
`InvitationService`. Keep status codes and frontend response shapes unchanged.
The full 256-test backend suite passed after this first extraction, including
existing journey and sharing access checks.

Persistence model tests use temporary in-memory SQLite with foreign keys enabled
and EnsureCreated only inside tests. Signup HTTP tests use migrations against
isolated temporary SQLite files. Neither changes the application's database.
