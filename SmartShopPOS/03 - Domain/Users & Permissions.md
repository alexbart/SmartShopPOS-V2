# Users & Permissions

Users and Permissions owns identities, permissions, roles, cashier access, terminal context, and user changes. Authorization should be permission-based and centralized at appropriate boundaries rather than scattered hard-coded role checks.

Important invariants: sensitive actions are authorized and audited; credentials and secrets are protected; failed logins and relevant user or permission changes are recorded without logging passwords or tokens.
