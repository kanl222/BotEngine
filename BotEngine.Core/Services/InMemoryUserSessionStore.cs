using System.Collections.Concurrent;
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BotEngine.Core.Services;

/// <summary>
/// Хранилище состояний диалогов в оперативной памяти.
/// Подходит для одиночного экземпляра приложения без горизонтального масштабирования.
/// Запускает фоновый таймер для очистки устаревших сессий каждые 5 минут.
/// </summary>
public sealed class InMemoryUserSessionStore : IUserSessionStore, IHostedService, IDisposable
{
    private sealed record Entry(UserDialogState State, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _store = new();
    private readonly ILogger<InMemoryUserSessionStore> _logger;
    private Timer? _cleanupTimer;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="InMemoryUserSessionStore"/>.
    /// </summary>
    /// <param name="logger">Логгер хранилища.</param>
    public InMemoryUserSessionStore(ILogger<InMemoryUserSessionStore> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Получает текущее состояние диалога пользователя.
    /// </summary>
    /// <param name="userId">Уникальный идентификатор пользователя.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Экземпляр <see cref="UserDialogState"/> или null, если состояние отсутствует или устарело.</returns>
    public Task<UserDialogState?> GetStateAsync(string userId, CancellationToken ct = default)
    {
        if (_store.TryGetValue(userId, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                return Task.FromResult<UserDialogState?>(entry.State);

            _store.TryRemove(userId, out _);
        }
        return Task.FromResult<UserDialogState?>(null);
    }

    /// <summary>
    /// Сохраняет или обновляет состояние диалога пользователя с заданным временем жизни.
    /// </summary>
    /// <param name="userId">Уникальный идентификатор пользователя.</param>
    /// <param name="state">Новое состояние диалога.</param>
    /// <param name="ttl">Время жизни сессии. По умолчанию — 10 минут.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Задача, представляющая процесс сохранения.</returns>
    public Task SetStateAsync(string userId, UserDialogState state, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        var expires = DateTimeOffset.UtcNow.Add(ttl ?? TimeSpan.FromMinutes(10));
        _store[userId] = new Entry(state, expires);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Удаляет сохраненное состояние диалога пользователя.
    /// </summary>
    /// <param name="userId">Уникальный идентификатор пользователя.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Задача, представляющая процесс удаления.</returns>
    public Task ClearStateAsync(string userId, CancellationToken ct = default)
    {
        _store.TryRemove(userId, out _);
        return Task.CompletedTask;
    }

    // ── IHostedService: фоновая очистка ───────────────────────────────────

    /// <summary>
    /// Запускает службу фоновой очистки сессий.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены запуска.</param>
    /// <returns>Задача запуска службы.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cleanupTimer = new Timer(
            callback: _ => Cleanup(),
            state: null,
            dueTime: TimeSpan.FromMinutes(5),
            period: TimeSpan.FromMinutes(5));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Останавливает службу фоновой очистки сессий.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены остановки.</param>
    /// <returns>Задача остановки службы.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _cleanupTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Освобождает ресурсы, используемые таймером очистки.
    /// </summary>
    public void Dispose() => _cleanupTimer?.Dispose();

    /// <summary>
    /// Проводит ревизию сохраненных сессий и удаляет те, время жизни которых истекло.
    /// </summary>
    private void Cleanup()
    {
        var now = DateTimeOffset.UtcNow;
        var removed = 0;
        foreach (var (key, entry) in _store)
        {
            if (entry.ExpiresAt <= now && _store.TryRemove(key, out _))
                removed++;
        }
        if (removed > 0)
            _logger.LogDebug("InMemoryUserSessionStore: очищено {Count} устаревших сессий.", removed);
    }
}
