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
    /// Ключ в Redis: <c>bot:session:v2:{sessionKey}</c>
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
            var raw = await _db.StringGetAsync(key).WaitAsync(ct);

            if (raw.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<UserDialogState>((string)raw!, JsonOpts);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: ошибка при чтении состояния сессии (SessionKey={SessionKey})", userId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SetStateAsync(string userId, UserDialogState state, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        try
        {
            var key = BuildKey(userId);
            var json = JsonSerializer.Serialize(state, JsonOpts);
            var saved = await _db.StringSetAsync(key, json, ttl ?? DefaultTtl).WaitAsync(ct);
            if (!saved)
                throw new RedisException($"Redis не подтвердил запись состояния '{key}'.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: ошибка при записи состояния сессии (SessionKey={SessionKey})", userId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ClearStateAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(BuildKey(userId)).WaitAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Redis: ошибка при удалении состояния сессии (SessionKey={SessionKey})", userId);
            throw;
        }
    }

    private static string BuildKey(string sessionKey) => $"bot:session:{sessionKey}";
}
