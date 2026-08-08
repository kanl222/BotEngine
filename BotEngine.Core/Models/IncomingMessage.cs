namespace BotEngine.Core.Models;

/// <summary>Входящее сообщение/событие от пользователя (платформо-независимое).</summary>
public sealed record IncomingMessage(
    string ChatId,
    string UserId,
    string Text,
    string? CallbackData,
    string Platform,
    string? MessageId = null)
{
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;

    /// <summary>Файловые вложения сообщения (пусто, если сообщение без файлов).</summary>
    public IReadOnlyList<BotFile> Files { get; init; } = Array.Empty<BotFile>();

    /// <summary>Возвращает <c>true</c>, если сообщение содержит хотя бы один файл.</summary>
    public bool HasFiles => Files.Count > 0;
}
