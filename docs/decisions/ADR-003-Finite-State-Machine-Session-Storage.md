# ADR-003: Finite State Machine (FSM) and User Session Storage

## Status
Superseded in part by ADR-005 and ADR-006

## Date
2026-08-17

## Context
Multi-step interactions (e.g. asking for user input, form filling, wizards) require maintaining conversational state between independent incoming updates from a user.

Key Requirements:
- High performance, non-blocking reads/writes per user.
- Automatic TTL (Time-To-Live) expiration to prevent stale state from capturing user input indefinitely.
- Background cleanup of expired sessions without thread pool starvation.
- Clear contract (`IUserSessionStore`) enabling distributed cache implementations (e.g. Redis) for clustered production deployments.

## Decision
1. **Abstraction (`IUserSessionStore`)**: Defines `GetStateAsync`, `SetStateAsync`, `ClearStateAsync` with `CancellationToken` support.
2. **State Model (`UserDialogState`)**: Defined as a lightweight `readonly record struct UserDialogState(string AwaitingInputFor)` containing the target command key and creation timestamp.
3. **Default In-Memory Implementation (`InMemoryUserSessionStore`)**:
   - Backed by `ConcurrentDictionary<string, Entry>` where `Entry` tracks `ExpiresAt`.
   - Uses modern `PeriodicTimer` inside `IHostedService` (`StartAsync` / `StopAsync`) running periodic cleanup every 5 minutes with cancellation token support.
   - Lazy on-access eviction when `GetStateAsync` encounters an expired record.

## Alternatives Considered

### Redis only
- Pros: Ready for horizontal scaling out of the box.
- Cons: Requires external infrastructure dependency for simple single-instance deployments or local testing.
- Rejected: Provide `InMemoryUserSessionStore` as default with clear interface for Redis extensions in production.

### Threading.Timer with lock
- Pros: Simple.
- Cons: Prone to timer overlapping and GC/disposal edge cases under load.
- Rejected: `PeriodicTimer` in .NET offers structured, cooperative cancellation and async loop execution.

## Consequences
- Multi-step wizards work seamlessly via `context.SetSessionAsync("command_name")` and `context.ClearSessionAsync()`.
- Expired sessions are evicted both eagerly on access and periodically in the background.
