using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
using Microsoft.Extensions.Logging;

namespace BotEngine.Core.Services;

/// <summary>
/// Диспетчер команд: пропускает сообщения через конвейер Middleware и маршрутизирует 
/// входящие сообщения (<see cref="IncomingMessage"/>) к соответствующей команде бота (<see cref="IBotCommand"/>).
/// </summary>
public sealed class CommandDispatcher : ICommandDispatcher
{
    private readonly ICommandFactory _factory;
    private readonly IUserSessionStore _sessions;
    private readonly IEnumerable<IBotMiddleware> _middlewares;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(
        ICommandFactory factory,
        IUserSessionStore sessions,
        IEnumerable<IBotMiddleware> middlewares,
        ILogger<CommandDispatcher> logger)
    {
        _factory = factory;
        _sessions = sessions;
        _middlewares = middlewares;
        _logger = logger;
    }

    public async Task DispatchAsync(IncomingMessage message, IMessagingPlatform platform, CancellationToken ct = default)
    {
        // Построение конвейера Middleware
        var middlewareList = _middlewares.ToList();
        BotMiddlewareDelegate targetPipeline = (msg, plt, token) => ExecuteCoreDispatchAsync(msg, plt, token);

        for (int i = middlewareList.Count - 1; i >= 0; i--)
        {
            var middleware = middlewareList[i];
            var next = targetPipeline;
            targetPipeline = (msg, plt, token) => middleware.InvokeAsync(msg, plt, next, token);
        }

        await targetPipeline(message, platform, ct);
    }

    private async Task ExecuteCoreDispatchAsync(IncomingMessage message, IMessagingPlatform platform, CancellationToken ct)
    {
        var context = new BotContext(
            ChatId: message.ChatId,
            UserId: message.UserId,
            Platform: message.Platform,
            MessagingPlatform: platform,
            Sessions: _sessions);

        _logger.LogInformation(
            "Маршрутизация сообщения: Platform={Platform}, ChatId={ChatId}, UserId={UserId}, Text='{Text}', CallbackData='{CallbackData}'",
            message.Platform, message.ChatId, message.UserId, message.Text, message.CallbackData);

        // 1. Обработка CallbackData (от нажатия Inline-кнопок)
        if (!string.IsNullOrEmpty(message.CallbackData))
        {
            var commandKey = message.CallbackData.Split(':', 2)[0].Trim().ToLowerInvariant();
            var command = _factory.Resolve(commandKey);
            if (command is not null)
            {
                await ExecuteSafeAsync(command, context, message, ct);
                return;
            }

            _logger.LogWarning("Неизвестный ключ callback-команды: '{Key}' (платформа: {Platform})", commandKey, message.Platform);
            await platform.SendTextAsync(message.ChatId, "Неизвестная действие. Нажмите /start для главного меню.", ct: ct);
            return;
        }

        // 2. Проверка активных сессий пользователя
        var sessionState = await _sessions.GetStateAsync(message.UserId, ct);
        if (sessionState is not null)
        {
            var sessionCommandKey = sessionState.AwaitingInputFor.Trim().ToLowerInvariant();
            var sessionCommand = _factory.Resolve(sessionCommandKey);
            if (sessionCommand is not null)
            {
                await ExecuteSafeAsync(sessionCommand, context, message, ct);
                return;
            }

            _logger.LogWarning("Сессия ссылается на неизвестную команду '{Key}'. Очистка устаревшей сессии.", sessionCommandKey);
            await _sessions.ClearStateAsync(message.UserId, ct);
        }

        // 3. Маршрутизация по явной команде (/start, /help, /ask)
        var text = message.Text?.Trim() ?? string.Empty;
        var commandName = text.StartsWith('/') ? text[1..] : text;
        var spaceIdx = commandName.IndexOf(' ');
        if (spaceIdx > 0) commandName = commandName[..spaceIdx];
        commandName = commandName.ToLowerInvariant();

        if (text.StartsWith('/') && !string.IsNullOrEmpty(commandName))
        {
            var namedCommand = _factory.Resolve(commandName);
            if (namedCommand is not null)
            {
                await ExecuteSafeAsync(namedCommand, context, message, ct);
                return;
            }
        }

        // 4. Фолбэк для естественного языка — отправляем в RAG-команду ("ask" или "default")
        var defaultCommand = _factory.Resolve("ask") ?? _factory.Resolve("default");
        if (defaultCommand is not null)
        {
            await ExecuteSafeAsync(defaultCommand, context, message, ct);
            return;
        }

        _logger.LogWarning("Обработчик для сообщения не найден (UserId: {UserId}, Platform: {Platform}): '{Text}'", message.UserId, message.Platform, message.Text);
        await platform.SendTextAsync(message.ChatId, "Команда не распознана. Воспользуйтесь /start для вызова главного меню.", ct: ct);
    }

    private async Task ExecuteSafeAsync(IBotCommand command, BotContext context, IncomingMessage message, CancellationToken ct)
    {
        try
        {
            await command.ExecuteAsync(context, message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необработанное исключение в команде '{CommandName}' (Platform={Platform}, UserId={UserId})",
                command.Name, message.Platform, message.UserId);
            try
            {
                await context.ReplyAsync("Произошла внутренняя ошибка. Попробуйте позже.", ct: ct);
            }
            catch
            {
                // Подавление вторичного исключения
            }
        }
    }
}
