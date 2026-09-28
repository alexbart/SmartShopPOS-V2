# Development Rules

- Build incrementally with small modules, clear names, explicit dependencies, and testable business logic.
- Use .NET 10, ASP.NET Core, React, TypeScript, Tailwind CSS, and PostgreSQL as the current technology direction.
- Use `decimal` for KES money and UTC internally for timestamps.
- Preserve stock movement and financial history; correct posted transactions through reversals, refunds, credit notes, or adjustments.
- Use transactions for multi-record business operations and database constraints for correctness and idempotency.
- Isolate M-Pesa, eTIMS, and hardware behind integration interfaces.
- Validate input, use permission-based authorization, audit important actions, and protect secrets.
- Add tests with business-critical features and verify work before reporting it.
- Preserve user work, avoid unrelated changes, and do not commit automatically.
