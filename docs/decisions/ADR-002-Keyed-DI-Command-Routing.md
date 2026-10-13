# ADR-002: Keyed Scoped DI and Command Dispatcher Pipeline

## Status
Superseded in part by ADR-004

## Date
2026-08-17

## Context
Incoming updates from platforms contain text commands (e.g. `/start`, `/help`), inline button callbacks (e.g. `ask:vpn`), user session state transitions, or natural language fallback requests. The engine needs a flexible, extensible routing pipeline that supports middlewares (logging, metrics, rate-limiting) and dependency injection.

Key Requirements:
- Commands must be resolved by name/key in O(1).
- Commands must support `Scoped` lifetime to allow injection of per-request dependencies (e.g. EF Core DbContext, user context) without memory leaks or captive dependencies.
- Pipeline should support composable middlewares (`IBotMiddleware`) executed before routing to the target command.

## Decision
1. **Keyed DI Registration**: Commands implement `IBotCommand` and are registered via `.AddKeyedScoped<IBotCommand, TCommand>("command_name")`.
2. **Scoped `ICommandFactory`**: `CommandFactory` is registered with `Scoped` lifetime and uses `IServiceProvider.GetKeyedService<IBotCommand>(name)` from the current message scope.
3. **Middleware Pipeline**: `CommandDispatcher` builds a nested delegate chain of `IBotMiddleware` components per incoming message.
4. **4-Stage Routing Strategy**:
   1. Inline Callback query (`CallbackData` prefix)
   2. Active Dialog Session (`UserDialogState.AwaitingInputFor`)
   3. Explicit slash command (`/start`, `/help`, etc.)
   4. Natural language fallback (`"ask"` or `"default"` command)

## Alternatives Considered

### Reflection-based Assembly Scanning
- Pros: Auto-registration without explicit `AddKeyedScoped`.
- Cons: Slower startup, harder to configure custom constructor parameters or conditionally disable commands.
- Rejected: Native Keyed DI is standard in modern .NET and provides explicit compile-time safety and container optimization.

### Singleton Command Instances
- Pros: Minimal allocation.
- Cons: Cannot inject scoped dependencies like database contexts or per-user scoped services.
- Rejected: Captive dependency antipattern and concurrency hazards with stateful services.

## Consequences
- Every incoming message creates a new DI scope via `IServiceScopeFactory`, ensuring all scoped services and commands are cleanly disposed after execution.
- Easy to add cross-cutting concerns by registering `IBotMiddleware` implementations in DI.
