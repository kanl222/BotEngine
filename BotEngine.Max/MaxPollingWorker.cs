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
    private readonly IMaxBotClient _client;
    private readonly MaxPlatformAdapter _adapter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaxPollingWorker> _logger;

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
            await dispatcher.DispatchAsync(message, _adapter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении команды (Platform=Max)");
        }
    }

    /// <summary>
    /// Выполняет фоновую задачу получения обновлений Long Polling от MAX API.
    /// </summary>
    /// <param name="stoppingToken">Токен отмены для остановки воркера.</param>
    /// <returns>Задача выполнения фоновой службы.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _client.Updates.PollUpdatesWithCallback(
                    callback: async (update, client) => await _adapter.HandleUpdateAsync(update),
                    limit: 100,
                    timeout: 90,
                    types: new List<string>
                    {
                        UpdateTypes.MessageCreated,
                        UpdateTypes.MessageCallback,
                        UpdateTypes.BotStarted
                    },
                    cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка соединения MAX API в Polling Worker. Повтор через 5 секунд...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
