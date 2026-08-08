namespace BotEngine.Core.Models;

/// <summary>
/// Определяет тип вложения-файла.
/// </summary>
public enum BotFileType
{
    /// <summary>
    /// Файл общего назначения (документ).
    /// </summary>
    Document,

    /// <summary>
    /// Фотография/изображение.
    /// </summary>
    Photo,

    /// <summary>
    /// Видеозапись.
    /// </summary>
    Video,

    /// <summary>
    /// Аудиофайл.
    /// </summary>
    Audio,

    /// <summary>
    /// Голосовое сообщение.
    /// </summary>
    Voice,

    /// <summary>
    /// Стикер.
    /// </summary>
    Sticker,

    /// <summary>
    /// Неизвестный или неподдерживаемый тип.
    /// </summary>
    Unknown
}

/// <summary>
/// Представляет платформо-независимое описание файлового вложения.
/// </summary>
/// <param name="FileId">Платформо-специфичный идентификатор или токен файла.</param>
/// <param name="FileName">Имя файла с расширением (если доступно).</param>
/// <param name="MimeType">MIME-тип вложения (если доступно).</param>
/// <param name="FileType">Тип вложения.</param>
/// <param name="FileSizeBytes">Размер файла в байтах (если известен).</param>
/// <param name="Url">Прямой URL для скачивания (если предоставляется платформой).</param>
public sealed record BotFile(
    string FileId,
    string? FileName,
    string? MimeType,
    BotFileType FileType,
    long? FileSizeBytes = null,
    string? Url = null);
