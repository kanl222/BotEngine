namespace BotEngine.Core.Models;

/// <summary>
/// Представляет состояние активного диалога пользователя (ожидание ввода).
/// </summary>
/// <param name="AwaitingInputFor">Имя команды, ожидающей ввода от пользователя.</param>
public record UserDialogState(string AwaitingInputFor)
{
    /// <summary>
    /// Возвращает время создания состояния сессии.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Произвольные данные диалога: позволяют передавать состояние между шагами команды.
    /// Например, сохранить промежуточный ввод пользователя до финального подтверждения.
    /// </summary>
    public Dictionary<string, string> Data { get; init; } = new();
}
