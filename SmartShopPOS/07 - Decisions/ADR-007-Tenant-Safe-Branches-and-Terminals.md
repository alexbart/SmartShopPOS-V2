# ADR-007: Tenant-Safe Branches and Terminals

**Status: Accepted**

## Context

Branches belong to organizations and terminals belong to branches. Application checks alone would not prevent a terminal row from pairing one organization's identifier with another organization's branch.

## Decision

Branches expose an alternate key on `(OrganizationId, Id)`. Terminals retain both `OrganizationId` and `BranchId`, and reference that branch key through a composite foreign key. This deliberate tenant-key duplication lets PostgreSQL enforce organization/branch consistency. Deletes are restricted; branch and terminal lifecycle changes use soft deactivation.

Management APIs derive the organization from the authenticated user and enforce global permission keys. Branch/terminal identifiers supplied in routes are selectors only and are always checked within that organization.

## Consequences

The database rejects cross-organization terminal associations even when application code is bypassed. Terminal queries can be scoped by organization and branch. Future hardware-agent identity and communication remain outside this domain model.
