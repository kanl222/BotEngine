using BotEngine.Core.Models;

namespace BotEngine.Core.Interfaces;

/// <summary>
/// Команда бота, адресованная конкретному чату.
/// </summary>
public interface IBotCommand
{
    /// <summary>
    /// Уникальное имя команды (например, <c>/start</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Выполняет команду в контексте указанного чата.
    /// </summary>
    /// <param name="context">Контекст бота: платформа, сессия и прочие зависимости.</param>
    /// <param name="message">Входящее сообщение, инициировавшее команду.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Задача, представляющая асинхронное выполнение команды.</returns>
    Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default);
}
