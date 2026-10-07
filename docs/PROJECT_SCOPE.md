# Confirmed project scope

Product context: **RouteWise Project Brief v3.0, 2 October 2026** (approved team
brief). The Phase 0 request governs implementation timing. The brief's broader
"Scaffold & Contracts" delivery stage is split into separate phases here;
contracts, mocks and backend patterns are not implemented during Phase 0.

## Product and journey rules

RouteWise helps travellers order known places within a single day. It targets
resizable desktop/PC windows; a mobile layout is not required.

- Maximum **10 total locations**, including Start and End/Return-to-Start.
- Places are added through search/autocomplete. Map-click entry is not required.
- Walking and Driving only; optimise by Total Distance or Total Travel Time.
- Start is always fixed. The three end modes are Return to Start, Fixed End and
  Open End. Open End allows the optimiser to choose the final selected destination.
- Custom C# Simulated Annealing chooses the order of intermediate destinations.
  Google supplies place/map and route-cost data, not RouteWise's visiting order.
- Return one recommended route. Edit its order through a draggable sidebar list.
  Manual reorder recalculates totals, numbered markers and the route line.
  Map markers are not directly dragged to reorder stops.
- The Phase 0 request additionally confirms reset to the original recommended
  route and an optional journey date. Reset is not a new optimisation run.

## Journeys and itinerary

- Enter a journey name on save. Owned saved journeys support search/filter/sort,
  Open, Rename, Duplicate, Delete and Edit.
- A duplicate is a new owned journey; sharing/invitation metadata is not copied.
- Journey Details shows an ordered itinerary with a visible/supporting map.
  The exact arrangement is not frozen.
- Notes are optional per destination. Optional Plan Times mode exposes manually
  entered planned times, which are never optimisation inputs.

## Accounts and sharing

- Sign Up/Login uses a unique username and password only; no email, real name,
  profile, password recovery or editing. Usernames are 3?20 ASCII letters/digits/
  underscores, case-insensitively unique; passwords require at least 8 characters.
- Share only saved journeys, inviting a RouteWise user by username.
- Account supports logout and confirmed hard deletion of the user, owned journeys
  and associated invitations/sharing relationships. Deleted usernames are reusable.
- Invitations are Pending, Accepted or Declined. Accepted journeys are view-only
  for the recipient and appear in Shared Trips, separate from owned Saved Journeys.
- Friend relationships and a profile system are not required.

## Planned views and flow

Plan a Journey (called Create Journey in the brief) -> Optimised Route -> manual
reorder if desired -> Save -> Journey Details / Itinerary and sharing.
Saved Journeys manages owned journeys; Shared Trips manages invitations and
accepted access. These are the five meaningful functional views. Authentication
screens are additional and do not count toward those five.

## Boundaries and assessment

Core business logic and database interactions stay in C# / ASP.NET Core. Planned
persistence is SQLite with EF Core and meaningful LINQ/lambda queries; testing is
NUnit. Later implementation must use meaningful validation, error handling, data
structures, polymorphism, at least two interfaces, a custom generic and real tests.
The brief maps these to optimisation objectives, route optimiser/cost interfaces
and a reusable service result, but Phase 0 creates none of these types.

Excluded: Flying/Public Transport, machine learning, ranked route alternatives,
a separate Re-optimise action after manual reorder, collaborative editing,
friends/profiles/chat/comments, bookings/flights, automatic schedule optimisation
or planned times influencing optimisation.

Algorithm tuning, cost lookup internals, database schema, exact validation wording
and internal backend organisation remain open to their owners. Scope changes
require team agreement.
