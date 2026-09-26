Run `dotnet run --project tests/Regression` from the repository root.

This dependency-free regression runner compiles the production traffic, journey and notification
classes against a small in-memory engine stub. It checks the traffic size threshold and empty lists,
location identity across list changes,
notification ownership, citizen/vehicle journeys, transit identity, stale selections and bounded
path traversal. It does not run Unity rendering or jobs;
the normal mod build verifies API compatibility, and visibility still needs an in-game check.
