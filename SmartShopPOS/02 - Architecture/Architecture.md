# Architecture

## Intended Application Architecture

```text
React + TypeScript Web POS
          |
          v
ASP.NET Core API
          |
          v
Application / Domain / Infrastructure
          |
          v
PostgreSQL
```

The browser is the POS and management experience. The API owns application access and coordinates domain and infrastructure concerns. PostgreSQL is the primary production database, with constraints and transactions contributing to correctness.

Organizations own branches, and branches own registered terminals. Branch management derives organization scope from the authenticated user. A terminal also stores `OrganizationId` so a composite foreign key can enforce that its branch belongs to the same organization.

## Operational Authorization Scopes

These scopes are separate and must not substitute for each other:

```text
Organization scope
    != Branch operational scope
    != Terminal scope
```

Organization scope is derived from the authenticated identity and limits tenant-owned records. Branch-operational operations additionally require the active server-side branch context and `IBranchAccessService.CanOperateInBranchAsync` with the operation's permission; database queries and mutations must include both organization and branch. Client-supplied IDs are selectors only. Terminal context is a later layer and must not be inferred from branch access.

## API Versioning

Public HTTP APIs use major URI versions such as `/api/v1/...`, with version-specific OpenAPI documents. Released versions keep backward-compatible contracts; breaking changes require a new major version. The existing unversioned routes predate this policy and are pre-stable: introduce the versioned routes before external clients depend on them, and document any compatibility period if old routes must remain available.

## Eventual Hardware Architecture

```text
Browser POS
    |
    v
Local Windows Hardware Agent
    |
    +--> Receipt Printer
    +--> Cash Drawer
    +--> Local Devices
```

The web application and hardware layer are separated so browser and core business logic do not depend on printer protocols, Windows queues, or device-specific behavior. The local agent will provide a controlled boundary for locally attached devices while the web platform remains deployable and manageable as a web application.

See [[Receipt Printer]], [[Cash Drawer]], and [[Barcode Scanner]].
