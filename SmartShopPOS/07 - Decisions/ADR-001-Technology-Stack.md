# ADR-001: Technology Stack

**Status: Accepted**

## Context

SmartShopPOS needs a maintainable web platform for Kenyan retail workflows, with a mobile-first POS experience and a strongly structured backend.

## Decision

Use .NET 10, ASP.NET Core, and C# for the backend; React and TypeScript for the frontend; and PostgreSQL as the primary database. Entity Framework Core is the planned data-access technology and Tailwind CSS is the planned frontend styling technology.

## Consequences

The team can use a supported, typed stack with clear API boundaries and a mature relational database. The stack requires discipline around API contracts, frontend/backend separation, and PostgreSQL operations.
