# Users & Permissions

Organizations are the tenant boundary. Users and roles belong to one organization; role names are configurable per organization. Permissions are global machine-readable keys, and roles receive permissions through explicit assignments. Authorization is permission-based, never a hard-coded role-name check.

Users store normalized email values for organization-scoped uniqueness and a password hash only. Password hashing, login, tokens/sessions, and authenticated endpoints remain deferred to the authentication task.

The persistence model enforces organization ownership with restricted organization deletion and composite organization/user and organization/role foreign keys on user-role assignments. Unique constraints protect organization codes, normalized user emails within an organization, role names within an organization, global permission keys, and both join tables. Permission checks evaluate active user, organization, role, and assigned permission without inspecting role names.

Implemented tables: `organizations`, `users`, `roles`, `permissions`, `user_roles`, and `role_permissions`. Migration: `CreateOrganizationIdentity`. The permission catalog is seeded deterministically; roles are not seeded because they require a real organization. Database application and persistence verification require the local `ConnectionStrings__DefaultConnection` environment variable.

Branches, terminals, branch-scoped user access, and session context remain future features. These will attach to the organization/user identity model to support audit attribution and cashier operations without making them part of this first slice.
