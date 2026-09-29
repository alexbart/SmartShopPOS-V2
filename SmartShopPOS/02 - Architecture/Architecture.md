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
