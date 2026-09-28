# ADR-002: PostgreSQL

**Status: Accepted**

## Context

The platform needs transactional consistency, relational integrity, auditable financial records, and database-enforced uniqueness and idempotency.

## Decision

Use PostgreSQL as the primary production database and run it natively during initial development.

## Consequences

Transactions, constraints, indexes, and relational modeling support correctness and auditability. Development and operations must maintain consistent PostgreSQL tooling and configuration. Docker is not introduced without a later concrete need.
