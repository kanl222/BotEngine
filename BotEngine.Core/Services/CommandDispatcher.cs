using System.Diagnostics;
using BotEngine.Core.Diagnostics;
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
    private readonly ISessionKeyProvider _sessionKeys;
    private readonly IEnumerable<IBotMiddleware> _middlewares;
    private readonly ILogger<CommandDispatcher> _logger;

    public CommandDispatcher(
        ICommandFactory factory,
        IUserSessionStore sessions,
        ISessionKeyProvider sessionKeys,
        IEnumerable<IBotMiddleware> middlewares,
        ILogger<CommandDispatcher> logger)
    {
        _factory = factory;
        _sessions = sessions;
        _sessionKeys = sessionKeys;
        _middlewares = middlewares;
        _logger = logger;
    }

    public async Task DispatchAsync(IncomingMessage message, IMessagingPlatform platform, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(platform);

        using var activity = BotEngineDiagnostics.StartDispatch(message);
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var middlewareList = _middlewares.ToList();
            BotMiddlewareDelegate targetPipeline = (msg, plt, token) => ExecuteCoreDispatchAsync(msg, plt, token);

            for (var i = middlewareList.Count - 1; i >= 0; i--)
            {
                var middleware = middlewareList[i];
                var next = targetPipeline;
                targetPipeline = (msg, plt, token) => middleware.InvokeAsync(msg, plt, next, token);
            }

            await targetPipeline(message, platform, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
        finally
        {
            BotEngineDiagnostics.RecordDispatchDuration(
                message.Platform,
                Stopwatch.GetElapsedTime(startedAt));
        }
    }

    private async Task ExecuteCoreDispatchAsync(IncomingMessage message, IMessagingPlatform platform, CancellationToken ct)
    {
        var context = new BotContext(
            ChatId: message.ChatId,
            UserId: message.UserId,
            Platform: message.Platform,
            MessagingPlatform: platform,
            Sessions: _sessions)
        {
            SessionKey = _sessionKeys.Create(message)
        };

        using var logScope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = message.CorrelationId,
            ["Platform"] = message.Platform,
            ["ChatId"] = message.ChatId,
            ["UserId"] = message.UserId
        });

        _logger.LogInformation(
            "Маршрутизация сообщения: TextLength={TextLength}, HasCallback={HasCallback}, FileCount={FileCount}",
            message.Text?.Length ?? 0,
            !string.IsNullOrEmpty(message.CallbackData),
            message.Files.Count);

        // 1. Обработка CallbackData (от нажатия Inline-кнопок)
        if (!string.IsNullOrEmpty(message.CallbackData))
        {
            var commandKey = message.CallbackData.Split(':', 2)[0].Trim().ToLowerInvariant();
            var command = _factory.Resolve(commandKey);
            if (command is not null)
            {
                BotEngineDiagnostics.RecordRoute(message.Platform, "callback");
                await ExecuteSafeAsync(command, context, message, ct).ConfigureAwait(false);
                return;
            }

            _logger.LogWarning("Неизвестный ключ callback-команды: '{Key}' (платформа: {Platform})", commandKey, message.Platform);
            await platform.SendTextAsync(message.ChatId, "Неизвестное действие. Нажмите /start для главного меню.", ct: ct).ConfigureAwait(false);
            return;
        }

        // 2. Явная slash-команда имеет приоритет над активным диалогом.
        var text = message.Text?.Trim() ?? string.Empty;
        var explicitCommandName = ParseExplicitCommand(text);
        if (explicitCommandName is not null)
        {
            var namedCommand = _factory.Resolve(explicitCommandName);
            if (namedCommand is not null)
            {
                BotEngineDiagnostics.RecordRoute(message.Platform, "explicit");
                await ExecuteSafeAsync(namedCommand, context, message, ct).ConfigureAwait(false);
                return;
            }
        }

        // 3. Проверка активных сессий пользователя
        var sessionState = await _sessions.GetStateAsync(context.SessionKey, ct).ConfigureAwait(false);
        if (sessionState is { } state)
        {
            var sessionCommandKey = state.AwaitingInputFor.Trim().ToLowerInvariant();
            var sessionCommand = _factory.Resolve(sessionCommandKey);
            if (sessionCommand is not null)
            {
                BotEngineDiagnostics.RecordRoute(message.Platform, "session");
                await ExecuteSafeAsync(sessionCommand, context, message, ct).ConfigureAwait(false);
                return;
            }

            _logger.LogWarning("Сессия ссылается на неизвестную команду '{Key}'. Очистка устаревшей сессии.", sessionCommandKey);
            await _sessions.ClearStateAsync(context.SessionKey, ct).ConfigureAwait(false);
        }

        // 4. Фолбэк для естественного языка — отправляем в RAG-команду ("ask" или "default")
        var defaultCommand = _factory.Resolve("ask") ?? _factory.Resolve("default");
        if (defaultCommand is not null)
        {
            BotEngineDiagnostics.RecordRoute(message.Platform, "fallback");
            await ExecuteSafeAsync(defaultCommand, context, message, ct).ConfigureAwait(false);
            return;
        }

        _logger.LogWarning(
            "Обработчик для сообщения не найден: TextLength={TextLength}",
            message.Text?.Length ?? 0);
        await platform.SendTextAsync(message.ChatId, "Команда не распознана. Воспользуйтесь /start для вызова главного меню.", ct: ct).ConfigureAwait(false);
    }

    private static string? ParseExplicitCommand(string text)
    {
        if (!text.StartsWith('/'))
            return null;

        var token = text[1..];
        var whitespaceIndex = token.IndexOfAny([' ', '\t', '\r', '\n']);
        if (whitespaceIndex >= 0)
            token = token[..whitespaceIndex];

        var botSuffixIndex = token.IndexOf('@');
        if (botSuffixIndex >= 0)
            token = token[..botSuffixIndex];

        token = token.Trim();
        return token.Length == 0 ? null : token.ToLowerInvariant();
    }

    private async Task ExecuteSafeAsync(IBotCommand command, BotContext context, IncomingMessage message, CancellationToken ct)
    {
        try
        {
            await command.ExecuteAsync(context, message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            BotEngineDiagnostics.RecordCommandFailure(message.Platform, command.Name, ex);
            _logger.LogError(ex, "Необработанное исключение в команде '{CommandName}' (Platform={Platform}, UserId={UserId})",
                command.Name, message.Platform, message.UserId);
            try
            {
                await context.ReplyAsync("Произошла внутренняя ошибка. Попробуйте позже.", ct: ct).ConfigureAwait(false);
            }
            catch
            {
                // Подавление вторичного исключения
            }
        }
    }
}
