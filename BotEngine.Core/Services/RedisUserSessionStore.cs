using System.Text.Json;
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BotEngine.Core.Services;

/// <summary>
/// Хранилище диалоговых состояний на базе Redis.
/// Поддерживает горизонтальное масштабирование: все экземпляры приложения
/// читают и пишут состояние из единого Redis-кластера.
/// </summary>
/// <remarks>
/// Ключ в Redis: <c>bot:session:{userId}</c>
/// Значение: JSON-сериализованный <see cref="UserDialogState"/>.
/// TTL задаётся при сохранении (по умолчанию 10 минут).
/// </remarks>
public sealed class RedisUserSessionStore : IUserSessionStore
{
    private readonly IDatabase _db;
    private readonly ILogger<RedisUserSessionStore> _logger;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RedisUserSessionStore"/>.
    /// </summary>
    /// <param name="connection">Мультиплексор соединений Redis.</param>
    /// <param name="logger">Логгер хранилища.</param>
    public RedisUserSessionStore(IConnectionMultiplexer connection, ILogger<RedisUserSessionStore> logger)
    {
        _db = connection.GetDatabase();
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UserDialogState?> GetStateAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var key = BuildKey(userId);
            var raw = await _db.StringGetAsync(key);

            if (raw.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<UserDialogState>(raw!, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: ошибка при чтении состояния сессии (UserId={UserId})", userId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetStateAsync(string userId, UserDialogState state, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var key = BuildKey(userId);
            var json = JsonSerializer.Serialize(state, JsonOpts);
            await _db.StringSetAsync(key, json, ttl ?? DefaultTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: ошибка при записи состояния сессии (UserId={UserId})", userId);
        }
    }

    /// <inheritdoc />
    public async Task ClearStateAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(BuildKey(userId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: ошибка при удалении состояния сессии (UserId={UserId})", userId);
        }
    }

    private static string BuildKey(string userId) => $"bot:session:{userId}";
}
