# ADR-003: Web-First Hardware Architecture

**Status: Accepted**

## Context

The POS must be available as a web application, while receipt printers and cash drawers are locally attached Windows devices with device-specific protocols.

## Decision

Use a web-first browser POS and management portal backed by the ASP.NET Core API. Isolate local device communication in a future Windows hardware agent, behind abstractions such as `IReceiptPrinter`.

## Consequences

Core business logic remains portable and testable, and the web application can support cloud management. Local deployment must operate and secure the hardware-agent boundary, and device behavior requires separate integration testing.
