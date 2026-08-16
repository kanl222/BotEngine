namespace BotEngine.Core.Models;

/// <summary>
/// Представляет состояние активного диалога пользователя (ожидание ввода).
/// </summary>
/// <param name="AwaitingInputFor">Имя команды, ожидающей ввода от пользователя.</param>
public readonly record struct UserDialogState(string AwaitingInputFor)
{
    /// <summary>
    /// Возвращает время создания состояния сессии.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
