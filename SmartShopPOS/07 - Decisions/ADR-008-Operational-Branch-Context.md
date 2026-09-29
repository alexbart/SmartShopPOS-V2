# ADR-008: Operational Branch Context

**Status: Accepted**

## Context

Authentication identifies a user and organization, but a user may be assigned to several branches. Putting a branch identifier in the authentication cookie would conflate identity with mutable operational context and require reauthentication to switch branches.

## Decision

Store the optional selected branch ID on the existing server-side `AuthenticationSession`. Do not issue a separate branch cookie or token. Context selection requires an active authenticated session, active user and organization, active branch, active `UserBranch` assignment, and the `branch_context.select` permission. Context reads and branch-scoped authorization repeat those checks; stale selections are cleared. Session revocation clears the selected branch.

## Consequences

Context is replaceable per session and is not trusted merely because a branch ID was supplied by a client. The database composite foreign key prevents a session from selecting a branch outside its organization, while application checks enforce active assignment and permission. Terminal selection remains a later operational slice.
