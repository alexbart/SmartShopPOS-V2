# Users & Permissions

Organizations are the tenant boundary. Users and roles belong to one organization; role names are configurable per organization. Permissions are global machine-readable keys, and roles receive permissions through explicit assignments. Authorization is permission-based, never a hard-coded role-name check.

Users store normalized email values for organization-scoped uniqueness and a password hash only. Passwords are hashed with PBKDF2 via the `IPasswordHasher` abstraction in the application layer and the concrete `Pbkdf2PasswordHasher` in infrastructure.

Authentication is cookie-based and server-side session-aware. The login flow validates the organization code and email together, verifies the password, creates a revocable `AuthenticationSession`, stores a hash of the session token rather than raw data, and issues a secure HttpOnly cookie. Authenticated requests resolve UserId and OrganizationId through trusted server-side identity claims. The organization boundary is not client-controlled after authentication.

The persistence model enforces organization ownership with restricted organization deletion and composite organization/user and organization/role foreign keys on user-role assignments. Unique constraints protect organization codes, normalized user emails within an organization, role names within an organization, global permission keys, both join tables, and session tokens. Permission checks evaluate active user, organization, role, and assigned permission without inspecting role names.

Implemented tables: `organizations`, `users`, `roles`, `permissions`, `user_roles`, `role_permissions`, and `authentication_sessions`. Migration: `CreateOrganizationIdentity` plus the auth/session extension migration. The permission catalog is seeded deterministically; roles are not seeded because they require a real organization. Database application and persistence verification require the local `ConnectionStrings__DefaultConnection` environment variable.

Branches, terminals, branch-scoped user access, and richer session management remain future features. These will attach to the organization/user identity model to support audit attribution and cashier operations without making them part of this initial authentication foundation.
