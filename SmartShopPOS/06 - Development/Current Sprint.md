# Current Sprint

## Milestone 4 - Branch and Terminal Foundation

**Current task:** Branch and terminal foundation

**Status:** Initial branch and terminal domain, tenant-safe PostgreSQL model, permission-scoped APIs, and PostgreSQL-backed tests are implemented.

**Current implementation:** Branches are organization-scoped; terminals are branch-scoped and retain the organization identifier needed for a composite tenant foreign key. Endpoints list/create branches and list/create terminals under a branch, with organization derived from the authenticated user and authorization based on global permission keys.

**Deferred:** Branch/terminal update and deactivation endpoints, branch-scoped user access, audit wiring, hardware integration, and operational workstation behavior.

**Next planned milestone:** Build the product catalog on the organization/branch/terminal foundation.
