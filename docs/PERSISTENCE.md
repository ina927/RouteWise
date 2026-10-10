# Persistence foundation (Nam)

This is a proposed internal EF Core/SQLite model based on PROJECT_SCOPE.md and
API_CONTRACTS.md. It does not change or approve the frontend contracts. The initial
migration creates the schema. Signup now uses this database; see
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
  proposed storage choice supporting retained history without a fourth status.
  Access must require Accepted AND !RemovedFromShared. Removal/history and reinvite
  policy still need team agreement; no unique invitation-pair rule is imposed yet.
- Database cascades delete owned journeys, their points/segments and invitations,
  plus invitations where a deleted user is sender or recipient. Other owners'
  journeys and other recipients survive.

Entities are not API response DTOs. Later mapping must return the existing camelCase
contracts and enum strings, reconstruct annotations, calculate access and summaries,
and never expose credentials. Representative photo selection remains unresolved.

Signup implements username validation/normalization, password hashing and a
server-managed cookie session. Journey/sharing endpoints still need input validation,
ownership checks (including sender = journey owner), and agreed route provenance
validation with Zoe. Required strings and enum properties alone do not validate
client input. Do not treat client route costs as verified.

Persistence model tests use temporary in-memory SQLite with foreign keys enabled
and EnsureCreated only inside tests. Signup HTTP tests use migrations against
isolated temporary SQLite files. Neither changes the application's database.
