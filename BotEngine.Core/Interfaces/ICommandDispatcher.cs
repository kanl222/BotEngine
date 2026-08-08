using BotEngine.Core.Models;

namespace BotEngine.Core.Interfaces;

/// <summary>
/// Диспетчер команд: маршрутизирует входящие сообщения (<see cref="IncomingMessage"/>)
/// к соответствующей команде бота (<see cref="IBotCommand"/>).
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Определяет команду по содержимому сообщения и выполняет её.
    /// </summary>
    /// <param name="message">Входящее сообщение для маршрутизации.</param>
    /// <param name="platform">Платформа обмена сообщениями, через которую поступило сообщение.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Задача, представляющая асинхронную диспетчеризацию.</returns>
    /// <exception cref="InvalidOperationException">
    /// Выбрасывается, если команда не найдена и отсутствует обработчик по умолчанию.
    /// </exception>
    Task DispatchAsync(IncomingMessage message, IMessagingPlatform platform, CancellationToken ct = default);
}
