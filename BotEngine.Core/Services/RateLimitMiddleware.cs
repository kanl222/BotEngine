using System.Collections.Concurrent;
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BotEngine.Core.Services;

/// <summary>
/// Настройки Rate Limiting Middleware.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "BotEngine:RateLimit";

    /// <summary>
    /// Максимальное число сообщений, разрешённых за <see cref="WindowSeconds"/> секунд.
    /// </summary>
    public int MaxMessages { get; set; } = 30;

    /// <summary>
    /// Ширина скользящего окна в секундах.
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Текст ответа пользователю при превышении лимита. Если <see langword="null"/> — ответ не отправляется.
    /// </summary>
    public string? ThrottleMessage { get; set; } = "⏳ Слишком много запросов. Пожалуйста, подождите немного.";
}

/// <summary>
/// Middleware для ограничения частоты входящих сообщений (Rate Limiting).
/// Использует скользящее окно на основе временных меток в памяти.
/// Лимит применяется <strong>по userId</strong> — не по chatId, 
/// чтобы корректно обрабатывать групповые чаты.
/// </summary>
public sealed class RateLimitMiddleware : IBotMiddleware
{
    // userId → очередь временных меток входящих сообщений
    private readonly ConcurrentDictionary<string, Queue<long>> _counters = new();
    private readonly RateLimitOptions _opts;
    private readonly ILogger<RateLimitMiddleware> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RateLimitMiddleware"/>.
    /// </summary>
    /// <param name="options">Настройки лимитера.</param>
    /// <param name="logger">Логгер.</param>
    public RateLimitMiddleware(IOptions<RateLimitOptions> options, ILogger<RateLimitMiddleware> logger)
    {
        _opts = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(IncomingMessage message, IMessagingPlatform platform, BotMiddlewareDelegate next, CancellationToken ct = default)
    {
        if (IsThrottled(message.UserId))
        {
            _logger.LogWarning(
                "Rate limit exceeded: UserId={UserId}, Platform={Platform}",
                message.UserId, message.Platform);

            if (_opts.ThrottleMessage is not null)
                await platform.SendTextAsync(message.ChatId, _opts.ThrottleMessage, ct: ct);

            return; // Прерываем конвейер — команда не выполняется
        }

        await next(message, platform, ct);
    }

    /// <summary>
    /// Проверяет, превышен ли лимит для данного пользователя, и регистрирует новый запрос.
    /// </summary>
    private bool IsThrottled(string userId)
    {
        var nowTicks = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowMs = _opts.WindowSeconds * 1000L;
        var cutoff = nowTicks - windowMs;

        var queue = _counters.GetOrAdd(userId, _ => new Queue<long>());

        lock (queue)
        {
            // Очищаем устаревшие записи за пределами окна
            while (queue.Count > 0 && queue.Peek() < cutoff)
                queue.Dequeue();

            if (queue.Count >= _opts.MaxMessages)
                return true;

            queue.Enqueue(nowTicks);
            return false;
        }
    }
}
