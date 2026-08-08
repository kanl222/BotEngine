using BotEngine.Core.Models;

namespace BotEngine.Core.Interfaces;

/// <summary>
/// Делегат вызова следующего middleware в конвейере обработки сообщений.
/// </summary>
public delegate Task BotMiddlewareDelegate(IncomingMessage message, IMessagingPlatform platform, CancellationToken ct);

/// <summary>
/// Интерфейс встроенных компонентов Middleware BotEngine для логирования, аутентификации и валидации.
/// </summary>
public interface IBotMiddleware
{
    /// <summary>
    /// Выполняет промежуточную обработку входящего сообщения.
    /// </summary>
    Task InvokeAsync(IncomingMessage message, IMessagingPlatform platform, BotMiddlewareDelegate next, CancellationToken ct = default);
}
