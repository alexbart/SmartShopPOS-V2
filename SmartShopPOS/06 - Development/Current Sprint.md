# Current Sprint

## Milestone 3 - Authentication and Session Foundation

**Current task:** Authentication and session foundation

**Status:** Implementation complete for the initial auth foundation; PostgreSQL-backed verification remains dependent on valid local credentials.

**Current implementation:** Cookie-based authentication, PBKDF2 password hashing, server-side session tracking with revocation and expiration, current-user abstraction, login/logout/me endpoints, and auth-session persistence model. The project continues to preserve organization-scoped identity and permission-based authorization without adding a second RBAC system.

**Deferred:** Full API-level verification against a live PostgreSQL database when a valid local `ConnectionStrings__DefaultConnection` is active; richer branch/terminal access, audit wiring, and later authorization policies remain future work.

**Next planned milestone:** Expand authentication into broader application authorization and audit usage.
