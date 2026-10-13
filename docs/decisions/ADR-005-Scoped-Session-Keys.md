# ADR-005: Scope sessions by bot, platform, chat, and user

Status: Accepted

The former `{platform}:{userId}` key mixed a user's private and group dialogs and
could collide when several bots shared Redis. The default `ISessionKeyProvider`
now creates a versioned key from `InstanceId`, platform, chat, and user while
preserving identifier case. Applications with different tenancy boundaries may
replace the provider through DI; changing `InstanceId` deliberately starts a new
session namespace.
