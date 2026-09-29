# Current Sprint

## Milestone 5 - User/Branch Access and Operational Context

**Current task:** User/branch access and operational branch context

**Status:** Explicit user-to-branch assignments, tenant-safe persistence, permission-scoped APIs, and per-session operational context are implemented with PostgreSQL-backed tests.

**Current implementation:** Assignment history is retained through deactivation and duplicate active assignments are database-constrained. `/api/me/branches` lists active assigned branches. `/api/me/branch-context` selects/reads a branch stored on the server-side authentication session; every context operation validates identity, tenant, active user/branch/assignment, and permission.

**Deferred:** Branch-scoped user access in future operational APIs, branch/terminal update and deactivation endpoints, audit wiring, terminal context, hardware integration, and operational workstation behavior.

**Next planned milestone:** Build the product catalog on the organization/branch/terminal and user-access foundation.
