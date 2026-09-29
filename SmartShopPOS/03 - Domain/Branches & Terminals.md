# Branches & Terminals

Branches belong to one organization. Branch codes are normalized to uppercase and unique within that organization. Branches can be deactivated but are not physically deleted.

Terminals belong to one branch and use codes unique within that branch. A terminal retains `OrganizationId` alongside `BranchId` so PostgreSQL can enforce that the referenced branch belongs to the same organization. Terminal identity is not a hardware integration; local device communication remains behind the future Windows hardware-agent boundary.

The API derives tenant scope from the authenticated user. Branch and terminal listing/creation use the global `branches.*` and `terminals.*` permission keys. Deactivation behavior exists in the domain; management endpoints are deferred.
