using BotEngine.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BotEngine.Telegram;

/// <summary>
/// Фоновый сервис (BackgroundService), отвечающий за получение обновлений от Telegram методом Long Polling.
/// </summary>
public sealed class TelegramPollingWorker : BackgroundService
{
    /// <summary>
    /// Типы обновлений, которые реально обрабатываются в <see cref="TelegramPlatformAdapter.MapUpdate"/>.
    /// Ограничение списка на стороне Telegram API снижает объём трафика и лишнюю нагрузку на
    /// обработчик — сервер не присылает обновления типов, которые всё равно были бы отброшены.
    /// </summary>
    private static readonly ReceiverOptions ReceiverOptions = new()
    {
        AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
    };

    private readonly ITelegramBotClient _client;
    private readonly TelegramPlatformAdapter _adapter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramPollingWorker> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TelegramPollingWorker"/>.
    /// </summary>
    /// <param name="client">Клиент Telegram Bot API.</param>
    /// <param name="adapter">Адаптер платформы Telegram.</param>
    /// <param name="scopeFactory">Фабрика для создания Scoped-контейнеров DI при обработке сообщений.</param>
    /// <param name="logger">Логгер сервиса.</param>
    public TelegramPollingWorker(
        ITelegramBotClient client,
        TelegramPlatformAdapter adapter,
        IServiceScopeFactory scopeFactory,
        ILogger<TelegramPollingWorker> logger)
    {
        _client = client;
        _adapter = adapter;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Выполняет фоновую задачу получения обновлений Long Polling.
    /// </summary>
    /// <param name="stoppingToken">Токен отмены для остановки воркера.</param>
    /// <returns>Задача выполнения фоновой службы.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Запуск фоновой службы Telegram Polling Worker...");

        try
        {
            _client.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: ReceiverOptions,
                cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Не удалось запустить Telegram Polling Worker");
            throw;
        }

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Корректная остановка сервиса
        }
    }

    /// <summary>
    /// Обрабатывает входящее обновление от Telegram.
    /// </summary>
    /// <param name="bot">Экземпляр клиента Telegram Bot API.</param>
    /// <param name="update">Объект входящего обновления.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Задача обработки обновления.</returns>
    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            var incoming = _adapter.MapUpdate(update);
            if (incoming is null)
                return;

            // Подтверждаем callback как можно раньше, чтобы у пользователя быстрее пропал
            // индикатор ожидания на кнопке — не дожидаясь завершения диспетчеризации команды.
            await _adapter.AcknowledgeCallbackAsync(update).ConfigureAwait(false);

            using var scope = _scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

            await dispatcher.DispatchAsync(incoming, _adapter, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Сервис останавливается — не считаем это ошибкой обработки обновления.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке обновления Telegram (UpdateId={UpdateId})", update.Id);
        }
    }

    /// <summary>
    /// Обрабатывает ошибки, возникающие при получении обновлений Long Polling.
    /// </summary>
    /// <param name="bot">Экземпляр клиента Telegram Bot API.</param>
    /// <param name="ex">Исключение, содержащее сведения об ошибке.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Задача завершения обработки ошибки.</returns>
    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "Ошибка соединения Telegram API в Polling Worker");
        return Task.CompletedTask;
    }
}