# ADR-006: Authentication and Session Architecture

**Status: Accepted**

## Context

The organization-scoped identity model provides the tenant boundary and permission catalog. The next requirement is a secure authentication foundation that can answer who the active user is, which organization they belong to, and how that state is carried across subsequent requests without trusting client-controlled tenant headers.

## Decision

The API uses ASP.NET Core cookie authentication with a revocable server-side session record. The browser receives an HttpOnly cookie for the session token, while the server stores a hash of that session token and the session metadata required for expiry and revocation decisions. Authentication establishes the trusted UserId and OrganizationId server-side and attaches them as claims to the authenticated principal. Authorization remains permission-based through the existing organization-scoped RBAC model rather than role-name checks.

## Consequences

- The browser does not receive or store long-lived bearer tokens in localStorage.
- Session revocation is enforced server-side, not just by deleting the browser cookie.
- Organization context is derived from the authenticated identity, not from client input.
- The permission infrastructure can be reused for future endpoint authorization policies without duplicating RBAC logic.
- Additional UI and payment modules can attach to this base authentication boundary without changing the underlying security model.
