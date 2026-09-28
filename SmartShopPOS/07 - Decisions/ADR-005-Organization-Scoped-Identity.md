# ADR-005: Organization-Scoped Identity and RBAC

**Status: Accepted**

## Context

Users, roles, and permissions are foundational to tenant isolation and future audit attribution. Roles must be configurable by each organization, while permissions need stable cross-organization identifiers.

## Decision

Organizations own users and roles. Permissions are global machine-readable keys. User-role assignments include the organization identifier and use composite foreign keys to ensure the assigned user and role belong to the same organization. Authorization evaluates assigned permissions, not role names. Organization deletion is restricted while identity records reference it.

## Consequences

Database constraints prevent cross-organization role assignments and duplicate organization-scoped identities/roles. Branch and terminal access can be added later through explicit relationships. Login, password hashing, tokens/sessions, and role bootstrap are deferred to focused follow-up work.
