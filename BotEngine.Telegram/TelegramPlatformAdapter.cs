using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
using BotEngine.Telegram.Mapping;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotEngine.Telegram;

/// <summary>
/// Адаптер платформы Telegram: реализует <see cref="IMessagingPlatform"/> через SDK Telegram.Bot.
/// </summary>
/// <remarks>
/// Регистрируется как Singleton. Маппинг входящих <see cref="Update"/> в <see cref="IncomingMessage"/>
/// осуществляется методом <see cref="MapUpdate"/>; вызов выполняется из <see cref="TelegramPollingWorker"/>.
/// </remarks>
public sealed class TelegramPlatformAdapter : IMessagingPlatform
{
    private readonly ITelegramBotClient _client;
    private readonly ILogger<TelegramPlatformAdapter> _logger;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="TelegramPlatformAdapter"/>.
    /// </summary>
    /// <param name="client">Клиент Telegram Bot API.</param>
    /// <param name="logger">Логгер адаптера.</param>
    public TelegramPlatformAdapter(ITelegramBotClient client, ILogger<TelegramPlatformAdapter> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Это событие никогда не вызывается изнутри адаптера: <see cref="TelegramPollingWorker"/>
    /// не подписывается на него, а вместо этого вызывает <see cref="MapUpdate"/> напрямую и
    /// самостоятельно диспетчеризует результат через <c>ICommandDispatcher</c>. Событие оставлено
    /// только ради соответствия контракту <see cref="IMessagingPlatform"/> (в отличие от MAX,
    /// где <c>MaxPlatformAdapter.HandleUpdateAsync</c> реально его поднимает). Если в будущем
    /// появится код, подписывающийся на <see cref="OnMessageReceived"/> у Telegram-адаптера,
    /// он не получит уведомлений — стоит держать это в уме при рефакторинге.
    /// </remarks>
#pragma warning disable CS0067 // Событие не вызывается изнутри класса — см. примечание выше
    public event Func<IncomingMessage, Task>? OnMessageReceived;
#pragma warning restore CS0067

    // ── Текст ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Сначала пробует отправить с <see cref="ParseMode.Markdown"/>.
    /// При ошибке форматирования повторяет отправку без разметки.
    /// </remarks>
    public async Task SendTextAsync(string chatId, string text, BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!TryParseChatId(chatId, out var chatIdLong, "отправки сообщения"))
            return;

        var markup = ToInlineMarkup(keyboard);

        await SendWithMarkdownFallbackAsync(
            markdownAction: () => _client.SendMessage(chatIdLong, text, parseMode: ParseMode.Markdown, replyMarkup: markup, cancellationToken: ct),
            plainAction: () => _client.SendMessage(chatIdLong, text, replyMarkup: markup, cancellationToken: ct),
            operationName: "отправки сообщения").ConfigureAwait(false);
    }

    // ── Геопозиция ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task SendLocationAsync(string chatId, double latitude, double longitude, CancellationToken ct = default)
    {
        if (!TryParseChatId(chatId, out var chatIdLong, "отправки геопозиции"))
            return;

        try
        {
            await _client.SendLocation(chatIdLong, (float)latitude, (float)longitude, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке геопозиции в чат {ChatId}", chatId);
        }
    }

    // ── Фото (по URL / file_id) ───────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Принимает как публичный URL изображения, так и Telegram file_id ранее загруженного файла.
    /// </remarks>
    public async Task SendPhotoAsync(string chatId, string photoUrlOrFileId, string? caption = null,
        BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!TryParseChatId(chatId, out var chatIdLong, "отправки фото"))
            return;

        var markup = ToInlineMarkup(keyboard);
        var parseMode = caption is not null ? ParseMode.Markdown : default(ParseMode);

        try
        {
            await _client.SendPhoto(chatIdLong, InputFile.FromString(photoUrlOrFileId),
                caption: caption, parseMode: parseMode, replyMarkup: markup, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке фото в чат {ChatId}", chatId);
        }
    }

    // ── Файлы: отправка ───────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Метод отправки выбирается автоматически по <paramref name="mimeType"/> и расширению
    /// <paramref name="fileName"/>: фото → <c>SendPhoto</c>, видео → <c>SendVideo</c>,
    /// аудио → <c>SendAudio</c>, всё остальное → <c>SendDocument</c>.
    /// </remarks>
    public async Task SendFileAsync(string chatId, Stream content, string fileName, string? mimeType = null,
        string? caption = null, BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!TryParseChatId(chatId, out var chatIdLong, "отправки файла"))
            return;

        var markup = ToInlineMarkup(keyboard);
        var parseMode = caption is not null ? ParseMode.Markdown : default(ParseMode);
        var inputFile = InputFile.FromStream(content, fileName);
        var kind = ResolveFileKind(mimeType, fileName);

        try
        {
            Task sendTask = kind switch
            {
                TelegramFileKind.Photo => _client.SendPhoto(chatIdLong, inputFile, caption: caption,
                    parseMode: parseMode, replyMarkup: markup, cancellationToken: ct),
                TelegramFileKind.Video => _client.SendVideo(chatIdLong, inputFile, caption: caption,
                    parseMode: parseMode, replyMarkup: markup, cancellationToken: ct),
                TelegramFileKind.Audio => _client.SendAudio(chatIdLong, inputFile, caption: caption,
                    parseMode: parseMode, replyMarkup: markup, cancellationToken: ct),
                _ => _client.SendDocument(chatIdLong, inputFile, caption: caption,
                    parseMode: parseMode, replyMarkup: markup, cancellationToken: ct)
            };

            await sendTask.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке файла '{FileName}' в чат {ChatId}", fileName, chatId);
        }
    }

    // ── Файлы: скачивание ─────────────────────────────────────────────────

    /// <inheritdoc />
    /// <remarks>
    /// Скачивает файл в <see cref="MemoryStream"/>. Вызывающая сторона несёт ответственность
    /// за освобождение возвращённого потока.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Выбрасывается, если Telegram API не вернул путь к файлу.
    /// </exception>
    public async Task<Stream> DownloadFileAsync(BotFile file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        var tgFile = await _client.GetFile(file.FileId, ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(tgFile.FilePath))
            throw new InvalidOperationException(
                $"Telegram не вернул FilePath для file_id={file.FileId}");

        var ms = new MemoryStream();
        await _client.DownloadFile(tgFile.FilePath, ms, ct).ConfigureAwait(false);
        ms.Position = 0;
        return ms;
    }

    // ── Редактирование ────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task EditTextAsync(string chatId, string messageId, string newText,
        BotKeyboard? keyboard = null, CancellationToken ct = default)
        => EditMessageAsync(chatId, messageId, newText, keyboard, ct);

    /// <inheritdoc />
    public async Task EditMessageAsync(string chatId, string messageId, string newText,
        BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!TryParseChatAndMessageId(chatId, messageId, out var chatIdLong, out var msgIdInt, "редактирования сообщения"))
            return;

        var markup = ToInlineMarkup(keyboard);

        try
        {
            await _client.EditMessageText(chatIdLong, msgIdInt, newText,
                parseMode: ParseMode.Markdown, replyMarkup: markup, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при редактировании сообщения {MessageId} в чате {ChatId}", messageId, chatId);
        }
    }

    /// <inheritdoc />
    public async Task EditMessageReplyMarkupAsync(string chatId, string messageId,
        BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (!TryParseChatAndMessageId(chatId, messageId, out var chatIdLong, out var msgIdInt, "редактирования кнопок"))
            return;

        var markup = ToInlineMarkup(keyboard);

        try
        {
            await _client.EditMessageReplyMarkup(chatIdLong, msgIdInt, replyMarkup: markup, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при редактировании кнопок сообщения {MessageId} в чате {ChatId}", messageId, chatId);
        }
    }

    // ── Удаление ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task DeleteMessageAsync(string chatId, string messageId, CancellationToken ct = default)
    {
        if (!TryParseChatAndMessageId(chatId, messageId, out var chatIdLong, out var msgIdInt, "удаления сообщения"))
            return;

        try
        {
            await _client.DeleteMessage(chatIdLong, msgIdInt, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении сообщения {MessageId} из чата {ChatId}", messageId, chatId);
        }
    }

    // ── Маппинг Update → IncomingMessage ──────────────────────────────────

    /// <summary>
    /// Преобразует входящее обновление Telegram в платформо-независимый <see cref="IncomingMessage"/>.
    /// </summary>
    /// <param name="update">Обновление от Telegram API.</param>
    /// <returns>
    /// <see cref="IncomingMessage"/> — при успешном маппинге;
    /// <see langword="null"/> — для неподдерживаемых типов обновлений.
    /// </returns>
    public IncomingMessage? MapUpdate(Update update)
    {
        switch (update.Type)
        {
            case UpdateType.Message when update.Message is { } msg:
            {
                var text = msg.Text ?? msg.Caption ?? string.Empty;
                var files = MapMessageFiles(msg);
                return new IncomingMessage(
                    ChatId: msg.Chat.Id.ToString(),
                    UserId: msg.From?.Id.ToString() ?? msg.Chat.Id.ToString(),
                    Text: text,
                    CallbackData: null,
                    Platform: "Telegram",
                    MessageId: msg.MessageId.ToString())
                {
                    Files = files
                };
            }

            case UpdateType.CallbackQuery when update.CallbackQuery is { } cq:
                return new IncomingMessage(
                    ChatId: GetCallbackChatId(cq),
                    UserId: cq.From.Id.ToString(),
                    Text: cq.Data ?? string.Empty,
                    CallbackData: cq.Data,
                    Platform: "Telegram",
                    MessageId: cq.Message?.MessageId.ToString());

            default:
                return null;
        }
    }

    /// <summary>
    /// Подтверждает получение callback-запроса, чтобы Telegram снял индикатор ожидания с кнопки.
    /// Безопасно вызывать для любого типа обновления — не-callback обновления игнорируются.
    /// </summary>
    /// <param name="update">Обновление от Telegram API.</param>
    public async Task AcknowledgeCallbackAsync(Update update)
    {
        if (update.Type != UpdateType.CallbackQuery || update.CallbackQuery is null)
            return;

        try
        {
            await _client.AnswerCallbackQuery(update.CallbackQuery.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось выполнить AnswerCallbackQuery для {CallbackQueryId}", update.CallbackQuery.Id);
        }
    }

    // ── Вспомогательные методы ─────────────────────────────────────────────

    /// <summary>
    /// Разбирает строковый идентификатор чата в числовой формат Telegram API, логируя ошибку при неудаче.
    /// </summary>
    private bool TryParseChatId(string chatId, out long chatIdLong, string operation)
    {
        if (long.TryParse(chatId, out chatIdLong) && chatIdLong != 0)
            return true;

        _logger.LogError("Некорректный chatId для {Operation}", operation);
        return false;
    }

    /// <summary>
    /// Разбирает идентификаторы чата и сообщения, требуемые операциями редактирования/удаления,
    /// логируя ошибку при неудаче.
    /// </summary>
    private bool TryParseChatAndMessageId(string chatId, string messageId, out long chatIdLong, out int msgIdInt, string operation)
    {
        var chatOk = long.TryParse(chatId, out chatIdLong) && chatIdLong != 0;
        var msgOk = int.TryParse(messageId, out msgIdInt);

        if (chatOk && msgOk)
            return true;

        _logger.LogError("Некорректный chatId или messageId для {Operation}", operation);
        return false;
    }

    /// <summary>
    /// Преобразует платформо-независимую клавиатуру в разметку Telegram, либо возвращает
    /// <see langword="null"/>, если клавиатура не задана.
    /// </summary>
    private static InlineKeyboardMarkup? ToInlineMarkup(BotKeyboard? keyboard)
        => keyboard is not null ? ButtonMapper.CreateInline(keyboard) : null;

    /// <summary>
    /// Выполняет отправку сообщения с Markdown-разметкой и, в случае ошибки, повторяет
    /// операцию без разметки.
    /// </summary>
    /// <param name="markdownAction">Действие отправки с Markdown-разметкой.</param>
    /// <param name="plainAction">Действие отправки без разметки (fallback).</param>
    /// <param name="operationName">Название операции для сообщений об ошибке.</param>
    private async Task SendWithMarkdownFallbackAsync(Func<Task> markdownAction, Func<Task> plainAction, string operationName)
    {
        try
        {
            await markdownAction().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ошибка {Operation} с Markdown. Повтор без разметки.", operationName);
            try
            {
                await plainAction().ConfigureAwait(false);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Не удалось выполнить {Operation} даже без Markdown", operationName);
            }
        }
    }

    /// <summary>
    /// Извлекает все файловые вложения из входящего Telegram-сообщения
    /// и возвращает их в виде списка <see cref="BotFile"/>.
    /// </summary>
    /// <param name="msg">Входящее сообщение Telegram.</param>
    /// <returns>Список вложений; пустой список, если вложений нет.</returns>
    private static IReadOnlyList<BotFile> MapMessageFiles(Message msg)
    {
        var files = new List<BotFile>();

        // Документ
        if (msg.Document is { } doc)
        {
            files.Add(new BotFile(
                FileId: doc.FileId,
                FileName: doc.FileName,
                MimeType: doc.MimeType,
                FileType: BotFileType.Document,
                FileSizeBytes: doc.FileSize));
        }

        // Фото (берём наибольший размер)
        if (msg.Photo is { Length: > 0 } photos)
        {
            var best = photos.MaxBy(p => p.Width * p.Height)!;
            files.Add(new BotFile(
                FileId: best.FileId,
                FileName: null,
                MimeType: "image/jpeg",
                FileType: BotFileType.Photo,
                FileSizeBytes: best.FileSize));
        }

        // Видео
        if (msg.Video is { } video)
        {
            files.Add(new BotFile(
                FileId: video.FileId,
                FileName: video.FileName,
                MimeType: video.MimeType,
                FileType: BotFileType.Video,
                FileSizeBytes: video.FileSize));
        }

        // Аудио
        if (msg.Audio is { } audio)
        {
            files.Add(new BotFile(
                FileId: audio.FileId,
                FileName: audio.FileName,
                MimeType: audio.MimeType,
                FileType: BotFileType.Audio,
                FileSizeBytes: audio.FileSize));
        }

        // Голосовое сообщение
        if (msg.Voice is { } voice)
        {
            files.Add(new BotFile(
                FileId: voice.FileId,
                FileName: null,
                MimeType: voice.MimeType,
                FileType: BotFileType.Voice,
                FileSizeBytes: voice.FileSize));
        }

        // Стикер
        if (msg.Sticker is { } sticker)
        {
            files.Add(new BotFile(
                FileId: sticker.FileId,
                FileName: null,
                MimeType: sticker.IsAnimated ? "application/x-tgsticker" : "image/webp",
                FileType: BotFileType.Sticker));
        }

        return files;
    }

    /// <summary>
    /// Определяет <c>chat_id</c> из callback-запроса.
    /// Если сообщение содержит идентификатор чата — возвращает его,
    /// иначе использует идентификатор пользователя.
    /// </summary>
    /// <param name="query">Объект callback-запроса.</param>
    /// <returns>Строковый идентификатор чата.</returns>
    private static string GetCallbackChatId(CallbackQuery query)
        => query.Message is { } msg && msg.Chat.Id != 0
            ? msg.Chat.Id.ToString()
            : query.From.Id.ToString();

    /// <summary>
    /// Категория исходящего файла, используемая для выбора метода отправки Telegram Bot API.
    /// </summary>
    private enum TelegramFileKind
    {
        Photo,
        Video,
        Audio,
        Document
    }

    /// <summary>
    /// Определяет категорию файла по MIME-типу или, если он не задан/не распознан, по расширению имени файла.
    /// </summary>
    /// <param name="mimeType">MIME-тип файла (может быть <see langword="null"/>).</param>
    /// <param name="fileName">Имя файла с расширением.</param>
    /// <returns>Категория файла для выбора метода отправки.</returns>
    private static TelegramFileKind ResolveFileKind(string? mimeType, string fileName)
    {
        if (mimeType is not null)
        {
            if (mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) return TelegramFileKind.Photo;
            if (mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) return TelegramFileKind.Video;
            if (mimeType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) return TelegramFileKind.Audio;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" => TelegramFileKind.Photo,
            ".mp4" or ".mov" or ".mkv" or ".webm" => TelegramFileKind.Video,
            ".mp3" or ".wav" or ".m4a" or ".ogg" or ".flac" => TelegramFileKind.Audio,
            _ => TelegramFileKind.Document
        };
    }
}