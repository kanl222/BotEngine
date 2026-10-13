using BotEngine.Core.Models;

namespace BotEngine.Core.Interfaces;

/// <summary>
/// Формирует ключ состояния диалога для входящего сообщения.
/// </summary>
public interface ISessionKeyProvider
{
    /// <summary>Создаёт ключ сессии, уникальный в пределах настроенной области.</summary>
    string Create(IncomingMessage message);
}
