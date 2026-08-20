using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
using BotEngine.Core.Services;
using MAX.Bot.Interfaces;
using MAX.Bot.Interfaces.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BotEngine.Max;

/// <summary>
/// Фоновый сервис (BackgroundService), отвечающий за получение обновлений от платформы MAX методом Long Polling.
/// </summary>
public sealed class MaxPollingWorker : BackgroundService
{
    /// <summary>
    /// Список типов обновлений, на которые подписан воркер. Вынесен в статическое поле,
    /// чтобы не пересоздавать коллекцию на каждой итерации цикла опроса.
    /// </summary>
    private static readonly List<string> SubscribedUpdateTypes = new()
    {
        UpdateTypes.MessageCreated,
        UpdateTypes.MessageCallback,
        UpdateTypes.BotStarted
    };

    private static readonly TimeSpan MinReconnectDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromMinutes(1);

    private readonly IMaxBotClient _client;
    private readonly MaxPlatformAdapter _adapter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaxPollingWorker> _logger;
    private CancellationToken _stoppingToken;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MaxPollingWorker"/>.
    /// </summary>
    /// <param name="client">Клиент MAX Bot API.</param>
    /// <param name="adapter">Адаптер платформы MAX.</param>
    /// <param name="scopeFactory">Фабрика для создания Scoped-контейнеров DI.</param>
    /// <param name="logger">Логгер сервиса.</param>
    public MaxPollingWorker(
        IMaxBotClient client,
        MaxPlatformAdapter adapter,
        IServiceScopeFactory scopeFactory,
        ILogger<MaxPollingWorker> logger)
    {
        _client = client;
        _adapter = adapter;
        _scopeFactory = scopeFactory;
        _logger = logger;

        _adapter.OnMessageReceived += HandleMessageAsync;
    }

    /// <summary>
    /// Обрабатывает входящее платформо-независимое сообщение от пользователя.
    /// </summary>
    /// <param name="message">Входящее сообщение.</param>
    /// <returns>Задача обработки сообщения.</returns>
    private async Task HandleMessageAsync(IncomingMessage message)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
            await dispatcher.DispatchAsync(message, _adapter, _stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Сервис останавливается — не считаем это ошибкой обработки команды.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении команды (Platform=Max, ChatId={ChatId})", message.ChatId);
        }
    }

    /// <summary>
    /// Выполняет фоновую задачу получения обновлений Long Polling от MAX API.
    /// </summary>
    /// <param name="stoppingToken">Токен отмены для остановки воркера.</param>
    /// <returns>Задача выполнения фоновой службы.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        var reconnectDelay = MinReconnectDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _client.Updates.PollUpdatesWithCallback(
                    callback: async (update, client) => await _adapter.HandleUpdateAsync(update, stoppingToken).ConfigureAwait(false),
                    limit: 100,
                    timeout: 90,
                    types: SubscribedUpdateTypes,
                    cancellationToken: stoppingToken).ConfigureAwait(false);

                // Успешный цикл опроса — сбрасываем задержку переподключения.
                reconnectDelay = MinReconnectDelay;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка соединения MAX API в Polling Worker. Повтор через {Delay}...", reconnectDelay);
                try
                {
                    await Task.Delay(reconnectDelay, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                // Экспоненциальный backoff с ограничением сверху, чтобы не заваливать API
                // запросами при продолжительной недоступности, но и не ждать бесконечно долго.
                reconnectDelay = TimeSpan.FromSeconds(Math.Min(reconnectDelay.TotalSeconds * 2, MaxReconnectDelay.TotalSeconds));
            }
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _adapter.OnMessageReceived -= HandleMessageAsync;
        base.Dispose();
    }
}