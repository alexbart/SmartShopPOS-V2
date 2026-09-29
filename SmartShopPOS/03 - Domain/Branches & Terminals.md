# Branches & Terminals

Branches belong to one organization. Branch codes are normalized to uppercase and unique within that organization. Branches can be deactivated but are not physically deleted.

Terminals belong to one branch and use codes unique within that branch. A terminal retains `OrganizationId` alongside `BranchId` so PostgreSQL can enforce that the referenced branch belongs to the same organization. Terminal identity is not a hardware integration; local device communication remains behind the future Windows hardware-agent boundary.

The API derives tenant scope from the authenticated user. Branch and terminal listing/creation use the global `branches.*` and `terminals.*` permission keys. Deactivation behavior exists in the domain; management endpoints are deferred.

## User Assignments and Operational Context

`UserBranch` records explicit user-to-branch access and is deactivated rather than deleted. A user can hold assignments to multiple branches. PostgreSQL composite foreign keys enforce that the assignment's user and branch share its organization; a partial unique index prevents duplicate active assignments.

Authentication identifies the user, organization, and session. Operational branch context is separate: the selected branch ID is stored on the server-side authentication session, never in the browser cookie. Selection and every context read validate the active session, user, organization, branch, active assignment, and `branch_context.select` permission. A later branch-scoped service must also validate its required permission and that the requested branch matches this selected context.

Assignment management uses `GET/POST /api/branches/{branchId}/users` and `DELETE /api/branches/{branchId}/users/{userId}`. The authenticated user can inspect accessible branches at `GET /api/me/branches` and read/select context at `GET/POST /api/me/branch-context`. Terminal selection and workstation behavior remain deferred.
