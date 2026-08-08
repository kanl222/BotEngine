using BotEngine.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace BotEngine.Core.Services;

/// <summary>
/// Фабрика команд — разрешает соответствующую команду бота (<see cref="IBotCommand"/>) по строковому имени через Keyed DI.
/// </summary>
public sealed class CommandFactory : ICommandFactory
{
    private readonly IServiceProvider _sp;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="CommandFactory"/>.
    /// </summary>
    /// <param name="sp">Провайдер служб DI.</param>
    public CommandFactory(IServiceProvider sp) => _sp = sp;

    /// <summary>
    /// Разрешает команду бота по ее имени.
    /// </summary>
    /// <param name="commandName">Имя команды.</param>
    /// <returns>Экземпляр команды <see cref="IBotCommand"/> или null, если команда не найдена.</returns>
    public IBotCommand? Resolve(string commandName)
        => _sp.GetKeyedService<IBotCommand>(commandName);
}
