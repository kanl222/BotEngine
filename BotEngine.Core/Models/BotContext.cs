using BotEngine.Core.Interfaces;

namespace BotEngine.Core.Models;

public record BotContext(
    string ChatId,
    string UserId,
    string Platform,
    IMessagingPlatform MessagingPlatform,
    IUserSessionStore Sessions)
{
    // ── Текстовые сообщения ────────────────────────────────────────────────

    public Task ReplyAsync(string text, BotKeyboard? keyboard = null)
        => MessagingPlatform.SendTextAsync(ChatId, text, keyboard);

    // ── Геопозиция ────────────────────────────────────────────────────────

    public Task SendLocationAsync(double latitude, double longitude)
        => MessagingPlatform.SendLocationAsync(ChatId, latitude, longitude);

    // ── Фото (по URL или токену) ───────────────────────────────────────────

    /// <summary>Отправить изображение по URL или платформенному токену/file_id.</summary>
    public Task SendPhotoAsync(string photoUrlOrFileId, string? caption = null, BotKeyboard? keyboard = null)
        => MessagingPlatform.SendPhotoAsync(ChatId, photoUrlOrFileId, caption, keyboard);

    // ── Файлы ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Отправить файл из потока. 
    /// Платформа сама определяет тип (документ / фото / видео) по MIME-типу и имени.
    /// </summary>
    public Task SendFileAsync(Stream content, string fileName, string? mimeType = null,
        string? caption = null, BotKeyboard? keyboard = null)
        => MessagingPlatform.SendFileAsync(ChatId, content, fileName, mimeType, caption, keyboard);

    /// <summary>
    /// Скачать вложение, полученное от пользователя, и вернуть поток с байтами.
    /// Вызывающая сторона несёт ответственность за освобождение потока.
    /// </summary>
    public Task<Stream> DownloadFileAsync(BotFile file, CancellationToken ct = default)
        => MessagingPlatform.DownloadFileAsync(file, ct);

    // ── Редактирование / удаление ──────────────────────────────────────────

    /// <summary>Отредактировать сообщение по его идентификатору.</summary>
    public Task EditAsync(string messageId, string newText, BotKeyboard? keyboard = null)
        => MessagingPlatform.EditTextAsync(ChatId, messageId, newText, keyboard);

    /// <summary>Удалить сообщение по его идентификатору.</summary>
    public Task DeleteAsync(string messageId)
        => MessagingPlatform.DeleteMessageAsync(ChatId, messageId);

    // ── Сессии ────────────────────────────────────────────────────────────

    /// <summary>Получить активное состояние диалога для текущего пользователя.</summary>
    public Task<UserDialogState?> GetSessionAsync(CancellationToken ct = default)
        => Sessions.GetStateAsync(UserId, ct);

    /// <summary>Установить состояние диалога для текущего пользователя.</summary>
    public Task SetSessionAsync(string awaitingInputFor, TimeSpan? ttl = null, CancellationToken ct = default)
        => Sessions.SetStateAsync(UserId, new UserDialogState(awaitingInputFor), ttl, ct);

    /// <summary>Сбросить (завершить) активный диалог для текущего пользователя.</summary>
    public Task ClearSessionAsync(CancellationToken ct = default)
        => Sessions.ClearStateAsync(UserId, ct);
}
