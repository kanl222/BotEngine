using System.Collections.Concurrent;
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Core.Services;

/// <summary>
/// Последовательно обрабатывает сообщения одного пользователя, не блокируя других пользователей.
/// </summary>
public sealed class UserConcurrencyMiddleware : IBotMiddleware
{
    private sealed class Gate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int References { get; set; }
    }

    private readonly ConcurrentDictionary<string, Gate> _gates = new();
    private readonly object _sync = new();

    public async Task InvokeAsync(
        IncomingMessage message,
        IMessagingPlatform platform,
        BotMiddlewareDelegate next,
        CancellationToken ct = default)
    {
        var key = $"{message.Platform}:{message.UserId}".ToLowerInvariant();
        Gate gate;

        lock (_sync)
        {
            gate = _gates.GetOrAdd(key, static _ => new Gate());
            gate.References++;
        }

        try
        {
            await gate.Semaphore.WaitAsync(ct);
            try
            {
                await next(message, platform, ct);
            }
            finally
            {
                gate.Semaphore.Release();
            }
        }
        finally
        {
            lock (_sync)
            {
                gate.References--;
                if (gate.References == 0 && _gates.TryRemove(key, out var removed))
                    removed.Semaphore.Dispose();
            }
        }
    }
}
