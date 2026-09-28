# SmartShopPOS Agent Constitution

## Project Direction

SmartShopPOS is a production-minded Kenyan retail POS and business management platform.

- Backend: .NET 10, C#, ASP.NET Core, Entity Framework Core
- Frontend: React, TypeScript, Tailwind CSS, mobile-first POS interface
- Database: PostgreSQL, running natively during initial development
- Environment: VS Code, Git, PowerShell or Git Bash

The system is a web application from day one:

```text
Browser POS / Management Portal
			-> ASP.NET Core API
			-> PostgreSQL
```

Local Windows hardware will eventually be accessed through a separate hardware agent. Hardware-specific behavior must remain behind clear interfaces and must not leak into core business logic.

## Domain Boundaries

Expected business areas include Sales/POS, Products/Catalog, Inventory, Purchases, Payments, Accounting, Cashier Shifts, Users/Roles/Permissions, Audit, Reports, M-Pesa, eTIMS/Tax, Hardware, and System/Configuration. Build these incrementally; do not implement every area at once.

## Engineering Rules

- Use `decimal` for monetary values. The initial currency is Kenyan Shilling (KES).
- Store timestamps in UTC and convert at presentation boundaries.
- Model inventory through traceable stock movements; preserve movement history.
- Use database transactions for operations spanning sales, payments, inventory, accounting, and audit records.
- Design external callbacks and integrations for idempotency, enforcing uniqueness at the database level where appropriate.
- Treat audit logging as a first-class requirement with actor, action, time, entity/reference, terminal, and reason where relevant.
- Use permission-based authorization, validate external input, and never commit secrets, tokens, credentials, or private keys.
- Expose a REST API with correct HTTP status codes. Establish Swagger/OpenAPI when API implementation begins.
- Use structured logging without passwords, tokens, payment secrets, or unnecessary sensitive personal information.
- Treat PostgreSQL constraints as part of correctness; do not rely only on application validation.
- Keep M-Pesa and eTIMS behind provider/integration boundaries such as `IPaymentProvider` and `IElectronicTaxProvider`.
- Keep receipt printing and cash drawer operations behind hardware abstractions such as `IReceiptPrinter`.
- Posted financial transactions are immutable. Use reversals, refunds, credit notes, or adjustments rather than destructive edits.
- Add automated tests alongside business-critical features, especially for accounting, inventory, sales, payments, authorization, idempotency, callbacks, shifts, and critical API behavior.
- Prefer simple code, clear names, small modules, explicit dependencies, and testable business logic. Avoid premature abstractions and optimization.
- Do not introduce Docker unless a later task has a concrete reason to require it.

## Agent Workflow

For every task:

1. Read this file and the relevant task/context notes.
2. Inspect the current branch, changes, and relevant existing code.
3. Identify and implement the smallest coherent scope.
4. Add or update tests where appropriate and verify the result.
5. Report changed files, checks performed, remaining risks, and a suggested commit message.

Preserve user work, avoid unrelated changes, and do not commit automatically unless explicitly instructed.
