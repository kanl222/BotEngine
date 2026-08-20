using BotEngine.Core.Interfaces;

namespace BotEngine.Core.Models;

public record BotContext(
    string ChatId,
    string UserId,
    string Platform,
    IMessagingPlatform MessagingPlatform,
    IUserSessionStore Sessions)
{
        /// <summary>
    /// Уникальный ключ сессии с изоляцией по платформе (исключает межплатформенные коллизии).
    /// </summary>
    public string SessionKey => $"{Platform}:{UserId}".ToLowerInvariant();


    // ── Текстовые сообщения ────────────────────────────────────────────────

    public Task ReplyAsync(string text, BotKeyboard? keyboard = null, CancellationToken ct = default)
        => MessagingPlatform.SendTextAsync(ChatId, text, keyboard, ct);

    // ── Геопозиция ────────────────────────────────────────────────────────

    public Task SendLocationAsync(double latitude, double longitude, CancellationToken ct = default)
        => MessagingPlatform.SendLocationAsync(ChatId, latitude, longitude, ct);

    // ── Фото (по URL или токену) ───────────────────────────────────────────

    /// <summary>Отправить изображение по URL или платформенному токену/file_id.</summary>
    public Task SendPhotoAsync(string photoUrlOrFileId, string? caption = null, BotKeyboard? keyboard = null, CancellationToken ct = default)
        => MessagingPlatform.SendPhotoAsync(ChatId, photoUrlOrFileId, caption, keyboard, ct);

    // ── Файлы ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Отправить файл из потока. 
    /// Платформа сама определяет тип (документ / фото / видео) по MIME-типу и имени.
    /// </summary>
    public Task SendFileAsync(Stream content, string fileName, string? mimeType = null,
        string? caption = null, BotKeyboard? keyboard = null, CancellationToken ct = default)
        => MessagingPlatform.SendFileAsync(ChatId, content, fileName, mimeType, caption, keyboard, ct);

    /// <summary>
    /// Скачать вложение, полученное от пользователя, и вернуть поток с байтами.
    /// Вызывающая сторона несёт ответственность за освобождение потока.
    /// </summary>
    public Task<Stream> DownloadFileAsync(BotFile file, CancellationToken ct = default)
        => MessagingPlatform.DownloadFileAsync(file, ct);

    // ── Редактирование / удаление ──────────────────────────────────────────

    /// <summary>Отредактировать сообщение по его идентификатору.</summary>
    public Task EditAsync(string messageId, string newText, BotKeyboard? keyboard = null, CancellationToken ct = default)
        => MessagingPlatform.EditMessageAsync(ChatId, messageId, newText, keyboard, ct);

    /// <summary>Отредактировать текст и клавиатуру сообщения по его идентификатору.</summary>
    public Task EditMessageAsync(string messageId, string newText, BotKeyboard? keyboard = null, CancellationToken ct = default)
        => MessagingPlatform.EditMessageAsync(ChatId, messageId, newText, keyboard, ct);

    /// <summary>Отредактировать только inline-клавиатуру сообщения по его идентификатору.</summary>
    public Task EditMessageReplyMarkupAsync(string messageId, BotKeyboard? keyboard = null, CancellationToken ct = default)
        => MessagingPlatform.EditMessageReplyMarkupAsync(ChatId, messageId, keyboard, ct);

    /// <summary>Удалить сообщение по его идентификатору.</summary>
    public Task DeleteAsync(string messageId, CancellationToken ct = default)
        => MessagingPlatform.DeleteMessageAsync(ChatId, messageId, ct);


    // ── Сессии ────────────────────────────────────────────────────────────


    /// <summary>
    /// Редактирует существующее сообщение при нажатии inline-кнопки (callback), 
    /// либо отправляет новое сообщение при текстовой команде.
    /// </summary>
    public  Task ReplyOrEditAsync(IncomingMessage message,string text,BotKeyboard? keyboard = null,CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(message.CallbackData) && !string.IsNullOrEmpty(message.MessageId))
        {
            return EditAsync(message.MessageId, text, keyboard, ct);
        }

        return ReplyAsync(text, keyboard, ct);
    }

    /// <summary>
    /// Редактирует сообщение, если указан идентификатор и флаг isCallback, иначе отправляет новое.
    /// </summary>
    public Task ReplyOrEditAsync(string? messageId,bool isCallback,string text,BotKeyboard? keyboard = null, CancellationToken ct = default)
    {
        if (isCallback && !string.IsNullOrEmpty(messageId))
        {
            return EditAsync(messageId, text, keyboard, ct);
        }
        return ReplyAsync(text, keyboard, ct);
    }

        // ── Сессии ────────────────────────────────────────────────────────────

    /// <summary>Получить активное состояние диалога для текущего пользователя.</summary>
    public Task<UserDialogState?> GetSessionAsync(CancellationToken ct = default)
        => Sessions.GetStateAsync(SessionKey, ct);

    /// <summary>Установить состояние диалога для текущего пользователя.</summary>
    public Task SetSessionAsync(string awaitingInputFor, Dictionary<string, string>? data = null, TimeSpan? ttl = null, CancellationToken ct = default)
        => Sessions.SetStateAsync(SessionKey, new UserDialogState(awaitingInputFor) { Data = data ?? new() }, ttl, ct);

    /// <summary>Сбросить (завершить) активный диалог для текущего пользователя.</summary>
    public Task ClearSessionAsync(CancellationToken ct = default)
        => Sessions.ClearStateAsync(SessionKey, ct);
}
