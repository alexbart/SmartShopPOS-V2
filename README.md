# SmartShopPOS

SmartShopPOS is a planned Kenyan retail point-of-sale and business management platform. It is intended for retailers who need a simple cashier experience backed by reliable inventory, accounting, permissions, auditability, payments, reporting, and hardware integration.

## Direction

The planned architecture is a React and TypeScript web POS and management portal backed by an ASP.NET Core REST API and PostgreSQL. A future local Windows hardware agent will isolate receipt printers, cash drawers, and other locally attached devices from the browser and core business logic.

The platform is designed to grow toward multiple users, terminals, branches, inventory, sales, accounting, payments, M-Pesa, eTIMS, reporting, local hardware, and cloud/web management.

## Technology

- .NET 10, C#, ASP.NET Core, Entity Framework Core
- React, TypeScript, Tailwind CSS
- PostgreSQL
- VS Code, Git, PowerShell or Git Bash

## Status

- **Implemented:** Project documentation and .NET solution foundations, organization/user/RBAC domain and PostgreSQL persistence model, initial permission catalog, and `CreateOrganizationIdentity` migration.
- **In development:** Authentication and login/session design; PostgreSQL migration application and database-backed identity verification require valid local credentials.
- **Planned:** Branches, terminals, catalog, sales, inventory, accounting, payments, integrations, reporting, and hardware agent.

## Planned Modules

Sales/POS, Products/Catalog, Inventory, Purchases, Payments, Accounting, Cashier Shifts, Users/Roles/Permissions, Audit, Reports, M-Pesa, eTIMS/Tax, Hardware, and System/Configuration.

## Development Philosophy

Build incrementally with clear boundaries, auditable financial and stock records, database-enforced correctness, automated tests alongside features, and minimal complexity. Implementation truth will be code, tests, migrations, and OpenAPI. The Obsidian vault preserves project context, requirements, architecture, decisions, domain rules, known issues, and development state rather than duplicating the codebase.

See `AGENTS.md` for the engineering constitution and `docs/` for documentation-area guidance.
