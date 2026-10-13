# ADR-006: Redis session failures are fail-fast

Status: Accepted

Treating a Redis outage as an empty or successfully written session corrupted
dialog flow without signalling the caller. Redis operations now log and rethrow
failures, propagate cancellation, and use the versioned `bot:session:v2:` key
space. Availability must be handled above the store rather than by silently
changing state semantics.
