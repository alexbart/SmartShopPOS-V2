# Architecture Documentation

Use this area for system boundaries, deployment views, integration boundaries, data flow, and technical architecture. Keep durable decisions in `docs/adr/` and implementation details in code and OpenAPI.

The current product catalog foundation keeps master data organization-scoped while branch context remains a separate operational concern. The product model intentionally excludes branch, inventory, pricing, and stock fields so those features can be introduced in dedicated future slices without polluting the core catalog design.
