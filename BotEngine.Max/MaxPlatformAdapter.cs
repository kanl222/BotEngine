using System.Linq;
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
using BotEngine.Max.Mapping;
using MAX.Bot.Interfaces;
using MAX.Bot.Interfaces.Models;
using MAX.Bot.Interfaces.Models.Attachment;
using MAX.Bot.Interfaces.Models.Request;
using MAX.Bot.Interfaces.Models.Request.Message;
using MAX.Bot.Interfaces.Models.Request.Message.Attachment;
using MAX.Bot.Interfaces.Models.Request.Message.Attachment.Payloads;
using Microsoft.Extensions.Logging;

namespace BotEngine.Max;

/// <summary>
/// Адаптер платформы MAX: реализует <see cref="IMessagingPlatform"/> через <see cref="IMaxBotClient"/>.
/// </summary>
public sealed class MaxPlatformAdapter : IMessagingPlatform
{
    private readonly IMaxBotClient _client;
    private readonly ILogger<MaxPlatformAdapter> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="MaxPlatformAdapter"/>.
    /// </summary>
    /// <param name="client">Клиент MAX Bot API.</param>
    /// <param name="logger">Логгер адаптера.</param>
    public MaxPlatformAdapter(IMaxBotClient client, ILogger<MaxPlatformAdapter> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc/>
    public event Func<IncomingMessage, Task>? OnMessageReceived;

    // ── Текст ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Сначала пытается отправить сообщение в формате Markdown.
    /// При возникновении ошибок форматирования повторяет отправку без разметки.
    /// </remarks>
    public async Task SendTextAsync(string chatId, string text, BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!TryParseChatId(chatId, out var chatIdLong, "отправки сообщения"))
            return;

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong,
            Text = text,
            Attachments = BuildAttachmentList(null, keyboard)
        };

        await SendWithMarkdownFallbackAsync(
            request,
            (req, token) => _client.Messages.SendMessageAsync(req, cancellationToken: token),
            "отправки сообщения",
            ct).ConfigureAwait(false);
    }

    // ── Геопозиция ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendLocationAsync(string chatId, double latitude, double longitude, CancellationToken ct = default)
    {
        if (!TryParseChatId(chatId, out var chatIdLong, "отправки геопозиции"))
            return;

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong,
            Attachments = new List<Attachment> { new LocationAttachment(latitude, longitude) }
        };

        try
        {
            await _client.Messages.SendMessageAsync(request, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке геопозиции в чат {ChatId}", chatId);
        }
    }

    // ── Фото (по URL/токену) ──────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendPhotoAsync(string chatId, string photoUrlOrFileId, string? caption = null,
        BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!TryParseChatId(chatId, out var chatIdLong, "отправки фото"))
            return;

        var primary = new ImageAttachment { Payload = new ImagePayload { Url = photoUrlOrFileId } };

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong,
            Text = caption,
            Format = caption is not null ? MessageFormat.Markdown : null,
            Attachments = BuildAttachmentList(primary, keyboard)
        };

        try
        {
            await _client.Messages.SendMessageAsync(request, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке фото в чат {ChatId}", chatId);
        }
    }

    // ── Файлы: отправка ───────────────────────────────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Выполняет отправку файла в два шага:
    /// <list type="number">
    ///   <item>Загружает поток на сервера MAX и получает токен вложения с помощью метода <c>UploadAsync</c>.</item>
    ///   <item>Отправляет сообщение со ссылкой на полученный токен вложения.</item>
    /// </list>
    /// </remarks>
    public async Task SendFileAsync(string chatId, Stream content, string fileName, string? mimeType = null,
        string? caption = null, BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!TryParseChatId(chatId, out var chatIdLong, "отправки файла"))
            return;

        try
        {
            var uploadType = ResolveUploadType(mimeType, fileName);

            _logger.LogDebug("Загрузка файла '{FileName}' (тип: {UploadType}) на MAX...", fileName, uploadType);

            var token = await _client.Attachments.UploadAsync(new UploadRequest
            {
                Type = uploadType,
                Content = content,
                FileName = fileName,
                ContentType = mimeType
            }, ct).ConfigureAwait(false);

            _logger.LogDebug("Файл '{FileName}' загружен, токен: {Token}", fileName, token);

            var primary = BuildAttachmentFromToken(uploadType, token, fileName);

            var request = new SendMessageRequest
            {
                ChatId = chatIdLong,
                Text = caption,
                Format = caption is not null ? MessageFormat.Markdown : null,
                Attachments = BuildAttachmentList(primary, keyboard)
            };

            await _client.Messages.SendMessageAsync(request, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке файла '{FileName}' в чат {ChatId}", fileName, chatId);
        }
    }

    // ── Редактирование ────────────────────────────────────────────────────

    /// <inheritdoc/>
    public Task EditTextAsync(string chatId, string messageId, string newText,
        BotKeyboard? keyboard = null, CancellationToken ct = default)
        => EditMessageAsync(chatId, messageId, newText, keyboard, ct);

    /// <inheritdoc/>
    public async Task EditMessageAsync(string chatId, string messageId, string newText,
        BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            _logger.LogError("Некорректный messageId для редактирования");
            return;
        }

        // chatId для операции редактирования не строго обязателен: MAX API
        // позволяет редактировать сообщение по одному лишь messageId, поэтому
        // при ошибке парсинга просто продолжаем без него, а не прерываем операцию.
        var hasChatId = long.TryParse(chatId, out var chatIdLong) && chatIdLong != 0;
        if (!hasChatId)
            _logger.LogDebug("chatId '{ChatId}' не распознан, редактирование выполняется только по messageId", chatId);

        var request = new SendMessageRequest
        {
            ChatId = hasChatId ? chatIdLong : null,
            Text = newText,
            Attachments = BuildAttachmentList(null, keyboard)
        };

        await SendWithMarkdownFallbackAsync(
            request,
            (req, token) => _client.Messages.EditMessageByIdAsync(messageId, req, cancellationToken: token),
            $"редактирования сообщения {messageId}",
            ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Подгружает текущее сообщение, чтобы сохранить его текст и уже существующие
    /// вложения (например, фото или файл), заменяя только клавиатуру. Это предотвращает
    /// случайную потерю медиа-вложений при изменении одних лишь кнопок.
    /// </remarks>
    public async Task EditMessageReplyMarkupAsync(string chatId, string messageId,
        BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            _logger.LogError("Некорректный messageId для редактирования кнопок");
            return;
        }

        try
        {
            var msg = await _client.Messages.GetMessageByIdAsync(messageId, ct).ConfigureAwait(false);
            var hasChatId = long.TryParse(chatId, out var chatIdLong) && chatIdLong != 0;

            var keyboardAttachment = keyboard is not null ? ButtonMapper.ToInlineKeyboardAttachment(keyboard) : null;

            // Сохраняем все вложения, кроме предыдущей клавиатуры (определяется по имени типа,
            // т.к. конкретный класс инлайн-клавиатуры не типизирован публично в этом контексте).
            var attachments = (msg.Body?.Attachments ?? Enumerable.Empty<Attachment>())
                .Where(a => !a.GetType().Name.Contains("Keyboard", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (keyboardAttachment is not null)
                attachments.Add(keyboardAttachment);

            var request = new SendMessageRequest
            {
                ChatId = hasChatId ? chatIdLong : null,
                Text = msg.Body?.Text,
                Attachments = attachments.Count > 0 ? attachments : null
            };

            await _client.Messages.EditMessageByIdAsync(messageId, request, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при редактировании кнопок сообщения {MessageId} в чате {ChatId}", messageId, chatId);
        }
    }

    // ── Удаление ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task DeleteMessageAsync(string chatId, string messageId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            _logger.LogError("Некорректный messageId для удаления");
            return;
        }

        try
        {
            await _client.Messages.DeleteMessageByIdAsync(messageId, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении сообщения {MessageId} из чата {ChatId}", messageId, chatId);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Восстанавливает объект вложения по метаданным <see cref="BotFile"/> и
    /// скачивает его во временный файл или напрямую в память через MAX SDK.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Выбрасывается, если вложение не удалось скачать или прочитать.</exception>
    public async Task<Stream> DownloadFileAsync(BotFile file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        // Пытаемся получить URL для скачивания через MAX SDK.
        // Для этого нужно восстановить объект Attachment из BotFile.
        var attachment = ReconstructAttachment(file);

        var result = await _client.Attachments.DownloadAttachmentAsync(
            attachment,
            options: null,
            cancellationToken: ct).ConfigureAwait(false);

        if (result.Content is { Length: > 0 })
            return new MemoryStream(result.Content);

        if (!string.IsNullOrWhiteSpace(result.SavedFilePath))
            return File.OpenRead(result.SavedFilePath);

        throw new InvalidOperationException(
            $"MAX SDK не вернул содержимое файла для BotFile(FileId={file.FileId})");
    }

    // ── Обработка входящих обновлений ─────────────────────────────────────

    /// <summary>
    /// Обрабатывает входящее низкоуровневое обновление MAX API, конвертирует его и передает обработчикам.
    /// </summary>
    /// <param name="update">Объект входящего обновления.</param>
    /// <returns>Задача обработки обновления.</returns>
    public async Task HandleUpdateAsync(Update update, CancellationToken ct = default)
    {
        try
        {
            var incoming = MapUpdate(update);
            if (incoming is null)
                return;

            if (OnMessageReceived is { } handler)
                await handler.Invoke(incoming).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при обработке события MAX update ({UpdateType})", update?.UpdateType);
        }
    }

    // ── Маппинг Update → IncomingMessage ──────────────────────────────────

    /// <summary>
    /// Выполняет преобразование входящего обновления MAX API в платформо-независимый <see cref="IncomingMessage"/>.
    /// </summary>
    /// <param name="update">Входящее обновление.</param>
    /// <returns>Платформо-независимое сообщение или null, если тип обновления не поддерживается.</returns>
    private IncomingMessage? MapUpdate(Update update)
    {
        return update switch
        {
            MessageCreatedUpdate created when created.Message?.Recipient is { ChatId: var chatId } =>
                BuildIncomingMessage(created.Message, chatId),

            MessageEditedUpdate edited when edited.Message?.Recipient is { ChatId: var chatId } =>
                BuildIncomingMessage(edited.Message, chatId),

            MessageCallbackUpdate callbackUpdate when callbackUpdate.Callback?.Payload is { } payload =>
                new IncomingMessage(
                    ChatId: callbackUpdate.Message?.Recipient?.ChatId.ToString()
                            ?? callbackUpdate.Callback.User?.Id.ToString()
                            ?? string.Empty,
                    UserId: callbackUpdate.Callback.User?.Id.ToString() ?? string.Empty,
                    Text: payload,
                    CallbackData: payload,
                    Platform: "Max",
                    MessageId: callbackUpdate.Message?.Body?.Mid),

            BotStartedUpdate botStarted =>
                new IncomingMessage(
                    ChatId: botStarted.ChatId.ToString(),
                    UserId: botStarted.User?.Id.ToString() ?? botStarted.ChatId.ToString(),
                    Text: "/start",
                    CallbackData: string.IsNullOrWhiteSpace(botStarted.Payload) ? null : botStarted.Payload,
                    Platform: "Max"),

            _ => null
        };
    }

    /// <summary>
    /// Строит <see cref="IncomingMessage"/> из объекта сообщения MAX API (общая логика
    /// для событий создания и редактирования сообщения).
    /// </summary>
    private static IncomingMessage BuildIncomingMessage(Message? message, long chatId) =>
        new IncomingMessage(
            ChatId: chatId.ToString(),
            UserId: message?.Sender?.Id.ToString() ?? chatId.ToString(),
            Text: message?.Body?.Text ?? string.Empty,
            CallbackData: null,
            Platform: "Max",
            MessageId: message?.Body?.Mid)
        {
            Files = MapAttachments(message?.Body?.Attachments)
        };

    // ── Вспомогательные методы ─────────────────────────────────────────────

    /// <summary>
    /// Разбирает строковый идентификатор чата в числовой формат MAX API, логируя ошибку при неудаче.
    /// </summary>
    /// <param name="chatId">Строковый идентификатор чата.</param>
    /// <param name="chatIdLong">Разобранный числовой идентификатор.</param>
    /// <param name="operation">Название операции для сообщения об ошибке.</param>
    /// <returns><c>true</c>, если идентификатор успешно разобран.</returns>
    private bool TryParseChatId(string chatId, out long chatIdLong, string operation)
    {
        if (long.TryParse(chatId, out chatIdLong) && chatIdLong != 0)
            return true;

        _logger.LogError("Некорректный chatId для {Operation}", operation);
        return false;
    }

    /// <summary>
    /// Формирует список вложений сообщения из основного вложения (например, фото или файла)
    /// и, опционально, инлайн-клавиатуры.
    /// </summary>
    private static List<Attachment>? BuildAttachmentList(Attachment? primary, BotKeyboard? keyboard)
    {
        if (primary is null && keyboard is null)
            return null;

        var attachments = new List<Attachment>(capacity: 2);
        if (primary is not null)
            attachments.Add(primary);
        if (keyboard is not null)
            attachments.Add(ButtonMapper.ToInlineKeyboardAttachment(keyboard));

        return attachments;
    }

    /// <summary>
    /// Выполняет отправку/редактирование сообщения с Markdown-разметкой и, в случае ошибки,
    /// повторяет операцию без разметки. Общая логика для <see cref="SendTextAsync"/> и
    /// <see cref="EditMessageAsync"/>.
    /// </summary>
    private async Task SendWithMarkdownFallbackAsync(
        SendMessageRequest request,
        Func<SendMessageRequest, CancellationToken, Task> action,
        string operationName,
        CancellationToken ct)
    {
        request.Format = MessageFormat.Markdown;

        try
        {
            await action(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка {Operation} с Markdown. Повтор без разметки.", operationName);
            try
            {
                request.Format = null;
                await action(request, ct).ConfigureAwait(false);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Не удалось выполнить {Operation} даже без Markdown", operationName);
            }
        }
    }

    /// <summary>
    /// Преобразует коллекцию низкоуровневых вложений MAX API в список <see cref="BotFile"/>.
    /// </summary>
    /// <param name="attachments">Коллекция вложений.</param>
    /// <returns>Список преобразованных файлов бота.</returns>
    private static IReadOnlyList<BotFile> MapAttachments(IEnumerable<Attachment>? attachments)
    {
        if (attachments is null) return Array.Empty<BotFile>();

        var result = new List<BotFile>();
        foreach (var a in attachments)
        {
            var botFile = a switch
            {
                FileAttachment fa => new BotFile(
                    FileId: fa.Payload.Token ?? fa.Payload.Url ?? string.Empty,
                    FileName: fa.Filename,
                    MimeType: null,
                    FileType: BotFileType.Document,
                    FileSizeBytes: fa.Size,
                    Url: fa.Payload.Url),

                ImageAttachment ia => new BotFile(
                    FileId: ia.Payload.Token ?? ia.Payload.Url ?? string.Empty,
                    FileName: null,
                    MimeType: GuessMimeType(ia.Payload.Url, "image/jpeg"),
                    FileType: BotFileType.Photo,
                    Url: ia.Payload.Url),

                VideoAttachment va => new BotFile(
                    FileId: va.Payload?.Token ?? string.Empty,
                    FileName: null,
                    MimeType: "video/mp4",
                    FileType: BotFileType.Video),

                AudioAttachment aa => new BotFile(
                    FileId: aa.Payload?.Token ?? string.Empty,
                    FileName: null,
                    MimeType: "audio/mpeg",
                    FileType: BotFileType.Audio),

                _ => null
            };

            if (botFile is not null && !string.IsNullOrEmpty(botFile.FileId))
                result.Add(botFile);
        }
        return result;
    }

    /// <summary>
    /// Пытается уточнить MIME-тип по расширению в URL, возвращая значение по умолчанию,
    /// если расширение не распознано или URL отсутствует.
    /// </summary>
    private static string GuessMimeType(string? url, string defaultMimeType)
    {
        if (string.IsNullOrEmpty(url))
            return defaultMimeType;

        var ext = Path.GetExtension(url).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".heic" => "image/heic",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => defaultMimeType
        };
    }

    /// <summary>
    /// Определяет тип загрузки MAX по MIME-типу и расширению файла.
    /// </summary>
    /// <param name="mimeType">MIME-тип файла.</param>
    /// <param name="fileName">Имя файла.</param>
    /// <returns>Тип загрузки в системе MAX.</returns>
    private static UploadType ResolveUploadType(string? mimeType, string fileName)
    {
        if (mimeType is not null)
        {
            if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return UploadType.Image;
            if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return UploadType.Video;
            if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return UploadType.Audio;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".heic" => UploadType.Image,
            ".mp4" or ".mov" or ".mkv" or ".webm" => UploadType.Video,
            ".mp3" or ".wav" or ".m4a" or ".ogg" or ".flac" => UploadType.Audio,
            _ => UploadType.File
        };
    }

    /// <summary>
    /// Создает объект Attachment для отправки, исходя из типа загрузки и токена.
    /// </summary>
    /// <param name="uploadType">Тип загруженного файла.</param>
    /// <param name="token">Токен вложения.</param>
    /// <param name="fileName">Имя файла.</param>
    /// <returns>Экземпляр вложения MAX API.</returns>
    private static Attachment BuildAttachmentFromToken(UploadType uploadType, string token, string fileName) =>
        uploadType switch
        {
            UploadType.Image => new ImageAttachment { Payload = new ImagePayload { Token = token } },
            UploadType.Video => new VideoAttachment { Payload = new VideoPayload { Token = token } },
            UploadType.Audio => new AudioAttachment { Payload = new AudioPayload { Token = token } },
            _ => new FileAttachment
            {
                Payload = new FilePayload { Token = token },
                Filename = fileName
            }
        };

    /// <summary>
    /// Восстанавливает объект вложения MAX API по его описанию в <see cref="BotFile"/>.
    /// </summary>
    /// <param name="file">Файл бота.</param>
    /// <returns>Специфичное вложение MAX API.</returns>
    private static Attachment ReconstructAttachment(BotFile file)
    {
        return file.FileType switch
        {
            BotFileType.Photo => new ImageAttachment
            {
                Payload = new ImagePayload { Token = file.FileId, Url = file.Url ?? file.FileId }
            },
            BotFileType.Video => new VideoAttachment
            {
                Payload = new VideoPayload { Token = file.FileId }
            },
            BotFileType.Audio or BotFileType.Voice => new AudioAttachment
            {
                Payload = new AudioPayload { Token = file.FileId }
            },
            _ => new FileAttachment
            {
                Payload = new FilePayload { Token = file.FileId, Url = file.Url },
                Filename = file.FileName
            }
        };
    }
}