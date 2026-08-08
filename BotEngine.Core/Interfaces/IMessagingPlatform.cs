using BotEngine.Core.Models;

namespace BotEngine.Core.Interfaces;

public interface IMessagingPlatform
{
    /// <summary>Отправить текстовое сообщение в чат.</summary>
    Task SendTextAsync(string chatId, string text, BotKeyboard? keyboard = null, CancellationToken ct = default);

    /// <summary>Отправить геопозицию в чат.</summary>
    Task SendLocationAsync(string chatId, double latitude, double longitude, CancellationToken ct = default);

    /// <summary>Отправить файл из потока. Платформа сама определяет тип вложения по MIME и имени файла.</summary>
    Task SendFileAsync(string chatId, Stream content, string fileName, string? mimeType = null,
        string? caption = null, BotKeyboard? keyboard = null, CancellationToken ct = default);

    /// <summary>
    /// Скачать файл по его <see cref="BotFile"/> и вернуть поток с содержимым.
    /// Вызывающая сторона несёт ответственность за освобождение потока.
    /// </summary>
    Task<Stream> DownloadFileAsync(BotFile file, CancellationToken ct = default);

    /// <summary>
    /// Отредактировать ранее отправленное сообщение.
    /// <para>Платформы, не поддерживающие редактирование, кидают <see cref="NotSupportedException"/>.</para>
    /// </summary>
    Task EditTextAsync(string chatId, string messageId, string newText, BotKeyboard? keyboard = null, CancellationToken ct = default)
        => Task.FromException(new NotSupportedException("EditTextAsync не поддерживается платформой."));

    /// <summary>
    /// Удалить сообщение из чата.
    /// <para>Платформы, не поддерживающие удаление, кидают <see cref="NotSupportedException"/>.</para>
    /// </summary>
    Task DeleteMessageAsync(string chatId, string messageId, CancellationToken ct = default)
        => Task.FromException(new NotSupportedException("DeleteMessageAsync не поддерживается платформой."));

    /// <summary>
    /// Отправить изображение (URL или file_id/token).
    /// <para>Платформы, не поддерживающие отправку фото, кидают <see cref="NotSupportedException"/>.</para>
    /// </summary>
    Task SendPhotoAsync(string chatId, string photoUrlOrFileId, string? caption = null, BotKeyboard? keyboard = null, CancellationToken ct = default)
        => Task.FromException(new NotSupportedException("SendPhotoAsync не поддерживается платформой."));

    /// <summary>Событие: входящее сообщение/callback от пользователя.</summary>
    event Func<IncomingMessage, Task>? OnMessageReceived;
}
