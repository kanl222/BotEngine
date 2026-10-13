# ADR-001: Platform-Agnostic Architecture (Ports & Adapters)

## Status
Accepted

## Date
2026-08-17

## Context
We need to build a multi-platform bot framework capable of supporting diverse messaging platforms (Telegram, MAX / VK Teams, and future messengers) without coupling business logic to vendor-specific SDKs or wire models.

Key Requirements:
- Business commands and multi-step dialog flows must be written once and run across all supported platforms.
- Native capabilities (inline keyboards, reply keyboards, location requests, attachments) must be unified into clean domain abstractions while allowing platform adapters to render them using target API primitives.
- Separation between core routing/state logic (`BotEngine.Core`) and platform adapters (`BotEngine.Telegram`, `BotEngine.Max`).

## Decision
Adopt the **Ports and Adapters (Hexagonal)** architecture:
1. **Core Port Abstraction (`IMessagingPlatform`)**: Defines platform capabilities (`SendTextAsync`, `SendLocationAsync`, `SendFileAsync`, `DownloadFileAsync`, `EditTextAsync`, `DeleteMessageAsync`).
2. **Domain Models**: Immutable `BotContext`, `IncomingMessage`, `BotKeyboard`, `readonly record struct BotButton`, and `BotFile`.
3. **Platform Adapters**: Each adapter (`TelegramPlatformAdapter`, `MaxPlatformAdapter`) implements `IMessagingPlatform`, maps incoming webhook/polling updates into `IncomingMessage`, and translates domain models into SDK types (`InlineKeyboardMarkup`, `SendMessageRequest`, etc.).

## Alternatives Considered

### Direct SDK Usage in Commands
- Pros: Easy access to messenger-specific features (e.g. Telegram-only games, payments).
- Cons: High coupling, duplicate command logic for each messenger, difficult to test.
- Rejected: Defeats the primary goal of write-once cross-platform bots.

### Unified Webhook / HTTP Gateway only
- Pros: Standard HTTP ingress.
- Cons: Some platforms (or local setups) require long polling; vendor SDKs handle retry, chunking, and protocol subtleties.
- Rejected: Adapters must support both Long Polling workers and potential webhook integrations.

## Consequences
- Business commands only depend on `BotEngine.Core`.
- New messengers can be supported by implementing a new `IMessagingPlatform` adapter package without changing existing commands.
- Platform-specific limits (e.g. callback payload length limits, Markdown flavor differences) are managed within respective adapters (with automatic Markdown fallback).
