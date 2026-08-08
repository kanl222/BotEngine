namespace BotEngine.Core.Interfaces;

/// <summary>
/// Фабрика команд — разрешает команду по имени через keyed DI.
/// </summary>
public interface ICommandFactory
{
    /// <summary>
    /// Возвращает команду, зарегистрированную под указанным именем.
    /// </summary>
    /// <param name="commandName">Имя команды (например, <c>/help</c>).</param>
    /// <returns>
    /// Экземпляр <see cref="IBotCommand"/>, если команда найдена;
    /// иначе <see langword="null"/>.
    /// </returns>
    IBotCommand? Resolve(string commandName);
}
