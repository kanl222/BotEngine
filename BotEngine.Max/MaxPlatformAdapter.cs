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
        if (!long.TryParse(chatId, out var chatIdLong) || chatIdLong == 0)
        {
            _logger.LogError("Некорректный chatId");
            return;
        }

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong,
            Text = text,
            Format = MessageFormat.Markdown,
            Attachments = keyboard is not null
                ? new List<Attachment> { ButtonMapper.ToInlineKeyboardAttachment(keyboard) }
                : null
        };

        try
        {
            await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка отправки сообщения с Markdown. Повтор без разметки.");
            try
            {
                request.Format = null;
                await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Не удалось отправить сообщение даже без Markdown");
            }
        }
    }

    // ── Геопозиция ────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendLocationAsync(string chatId, double latitude, double longitude, CancellationToken ct = default)
    {
        if (!long.TryParse(chatId, out var chatIdLong) || chatIdLong == 0)
        {
            _logger.LogError("Некорректный chatId для отправки геопозиции");
            return;
        }

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong,
            Attachments = new List<Attachment>
            {
                new LocationAttachment(latitude, longitude)
            }
        };

        try
        {
            await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке геопозиции");
        }
    }

    // ── Фото (по URL/токену) ──────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SendPhotoAsync(string chatId, string photoUrlOrFileId, string? caption = null,
        BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!long.TryParse(chatId, out var chatIdLong) || chatIdLong == 0)
        {
            _logger.LogError("Некорректный chatId для отправки фото");
            return;
        }

        var attachments = new List<Attachment>
        {
            new ImageAttachment { Payload = new ImagePayload { Url = photoUrlOrFileId } }
        };

        if (keyboard is not null)
            attachments.Add(ButtonMapper.ToInlineKeyboardAttachment(keyboard));

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong,
            Text = caption,
            Format = caption is not null ? MessageFormat.Markdown : null,
            Attachments = attachments
        };

        try
        {
            await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
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
        if (!long.TryParse(chatId, out var chatIdLong) || chatIdLong == 0)
        {
            _logger.LogError("Некорректный chatId для отправки файла");
            return;
        }

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
            }, ct);

            _logger.LogDebug("Файл '{FileName}' загружен, токен: {Token}", fileName, token);

            var attachment = BuildAttachmentFromToken(uploadType, token, fileName);

            var attachments = new List<Attachment> { attachment };
            if (keyboard is not null)
                attachments.Add(ButtonMapper.ToInlineKeyboardAttachment(keyboard));

            var request = new SendMessageRequest
            {
                ChatId = chatIdLong,
                Text = caption,
                Format = caption is not null ? MessageFormat.Markdown : null,
                Attachments = attachments
            };

            await _client.Messages.SendMessageAsync(request, cancellationToken: ct);
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

        long.TryParse(chatId, out var chatIdLong);

        var request = new SendMessageRequest
        {
            ChatId = chatIdLong != 0 ? chatIdLong : null,
            Text = newText,
            Format = MessageFormat.Markdown,
            Attachments = keyboard is not null
                ? new List<Attachment> { ButtonMapper.ToInlineKeyboardAttachment(keyboard) }
                : null
        };

        try
        {
            await _client.Messages.EditMessageByIdAsync(messageId, request, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка редактирования сообщения с Markdown. Повтор без разметки.");
            try
            {
                request.Format = null;
                await _client.Messages.EditMessageByIdAsync(messageId, request, cancellationToken: ct);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Не удалось отредактировать сообщение {MessageId} в чате {ChatId}", messageId, chatId);
            }
        }
    }

    /// <inheritdoc/>
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
            var msg = await _client.Messages.GetMessageByIdAsync(messageId, ct);
            long.TryParse(chatId, out var chatIdLong);

            var request = new SendMessageRequest
            {
                ChatId = chatIdLong != 0 ? chatIdLong : null,
                Text = msg.Body?.Text,
                Attachments = keyboard is not null
                    ? new List<Attachment> { ButtonMapper.ToInlineKeyboardAttachment(keyboard) }
                    : null
            };

            await _client.Messages.EditMessageByIdAsync(messageId, request, cancellationToken: ct);
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
            await _client.Messages.DeleteMessageByIdAsync(messageId, cancellationToken: ct);
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

        // Пытаемся получить URL для скачивания через MAX SDK
        // Для этого нужно восстановить объект Attachment из BotFile
        var attachment = ReconstructAttachment(file);

        var result = await _client.Attachments.DownloadAttachmentAsync(
            attachment,
            options: null,
            cancellationToken: ct);

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
    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            var incoming = MapUpdate(update);
            if (incoming is null)
                return;

            if (OnMessageReceived is { } handler)
                await handler.Invoke(incoming);
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
                new IncomingMessage(
                    ChatId: chatId.ToString(),
                    UserId: created.Message.Sender?.Id.ToString() ?? chatId.ToString(),
                    Text: created.Message.Body?.Text ?? string.Empty,
                    CallbackData: null,
                    Platform: "Max",
                    MessageId: created.Message.Body?.Mid)
                {
                    Files = MapAttachments(created.Message.Body?.Attachments)
                },

            MessageEditedUpdate edited when edited.Message?.Recipient is { ChatId: var chatId } =>
                new IncomingMessage(
                    ChatId: chatId.ToString(),
                    UserId: edited.Message.Sender?.Id.ToString() ?? chatId.ToString(),
                    Text: edited.Message.Body?.Text ?? string.Empty,
                    CallbackData: null,
                    Platform: "Max",
                    MessageId: edited.Message.Body?.Mid)
                {
                    Files = MapAttachments(edited.Message.Body?.Attachments)
                },

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

    // ── Вспомогательные методы ─────────────────────────────────────────────

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
                    MimeType: "image/jpeg",
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
