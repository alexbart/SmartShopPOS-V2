# Development Rules

- Build incrementally with small modules, clear names, explicit dependencies, and testable business logic.
- Use .NET 10, ASP.NET Core, React, TypeScript, Tailwind CSS, and PostgreSQL as the current technology direction.
- Use `decimal` for KES money and UTC internally for timestamps.
- Preserve stock movement and financial history; correct posted transactions through reversals, refunds, credit notes, or adjustments.
- Use transactions for multi-record business operations and database constraints for correctness and idempotency.
- Isolate M-Pesa, eTIMS, and hardware behind integration interfaces.
- Validate input, use permission-based authorization, audit important actions, and protect secrets.
- Resolve operational scope explicitly: `Organization scope != Branch operational scope != Terminal scope`. Organization ownership is derived from the authenticated identity. Branch-specific application operations must validate the selected server-side branch with `IBranchAccessService.CanOperateInBranchAsync(branchId, requiredPermission, ...)` and include both authenticated organization and branch in database filters and mutations. A route/body ID is only a selector, never proof of access. Do not add terminal context until its operational slice is designed.
- Version public HTTP APIs with major URI segments (`/api/v1/...`) and expose version-specific OpenAPI documents. Released versions remain compatible; breaking contract changes require a new major version. Existing unversioned endpoints are pre-stable and must be migrated/versioned before they acquire external consumers.
- Add tests with business-critical features and verify work before reporting it.
- Preserve user work, avoid unrelated changes, and do not commit automatically.
