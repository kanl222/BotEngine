using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
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
        UpdateTypes.BotStarted,
        UpdateTypes.MessageEdited,
        UpdateTypes.CommentCreated
    };

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
        _client.Updates.OnPollingError += HandlePollingErrorAsync;
    }

    /// <summary>
    /// Обрабатывает входящее платформо-независимое сообщение от пользователя.
    /// Запускает диспетчеризацию изолированно в пуле задач, исключая Head-of-Line Blocking очереди обновлений.
    /// </summary>
    /// <param name="message">Входящее сообщение.</param>
    /// <returns>Завершённая задача.</returns>
    private Task HandleMessageAsync(IncomingMessage message)
    {
        _ = ProcessMessageInBackgroundAsync(message);
        return Task.CompletedTask;
    }

    private async Task ProcessMessageInBackgroundAsync(IncomingMessage message)
    {
        try
        {
            using var logScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = message.CorrelationId,
                ["Platform"] = message.Platform,
                ["ChatId"] = message.ChatId,
                ["UserId"] = message.UserId
            });

            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
            await dispatcher.DispatchAsync(message, _adapter, _stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            // Штатная отмена при остановке хоста
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при выполнении команды (Platform=Max, CorrelationId={CorrelationId}, ChatId={ChatId})", message.CorrelationId, message.ChatId);
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

        try
        {
            await _client.Updates.PollUpdatesWithCallback(
                callback: (update, _) => _adapter.HandleUpdateAsync(update),
                limit: 100,
                timeout: 25,
                marker: null,
                types: SubscribedUpdateTypes,
                parallelProcessing: true,
                cancellationToken: stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Корректная остановка сервиса.
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "MAX polling worker неожиданно завершился");
            throw;
        }
    }

    private Task HandlePollingErrorAsync(Exception exception)
    {
        _logger.LogWarning(
            exception,
            "Ошибка соединения MAX API. SDK выполнит повтор с экспоненциальной задержкой");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.StopPollingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Подавление ошибок завершения опроса
        }

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _adapter.OnMessageReceived -= HandleMessageAsync;
        _client.Updates.OnPollingError -= HandlePollingErrorAsync;
        base.Dispose();
    }
}
