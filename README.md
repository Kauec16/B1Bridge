# B1Bridge

Modular integration platform for SAP Business One, built with .NET.

## Current stage

This branch establishes the initial solution structure. The integration flow, messaging, persistence, and SAP adapters will be implemented incrementally in the following branches.

## Foundation

- `B1Bridge.Api`: initial HTTP host for administration endpoints.
- `B1Bridge.Application`: application use cases and orchestration boundaries.
- `B1Bridge.Domain`: business rules and integration models.
- `B1Bridge.Infrastructure`: external adapters and technical implementations.

The current foundation intentionally contains no business integration endpoint yet.
