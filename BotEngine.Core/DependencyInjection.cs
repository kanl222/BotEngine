using BotEngine.Core.Interfaces;
using BotEngine.Core.Services;
// Предположим, что эти пространства имен пришли из других проектов
using Microsoft.Extensions.DependencyInjection;

namespace BotEngine.Core;

/// <summary>
/// Расширения DI-контейнера для регистрации ядра BotEngine.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Регистрирует все сервисы BotEngine:
    /// <list type="bullet">
    ///   <item><see cref="ICommandFactory"/> → <see cref="CommandFactory"/> (Singleton)</item>
    ///   <item><see cref="ICommandDispatcher"/> → <see cref="CommandDispatcher"/> (Scoped)</item>
    ///   <item><see cref="IUserSessionStore"/> → <see cref="InMemoryUserSessionStore"/> (Singleton + HostedService)</item>
    ///   <item>... сервисы из BotEngine.Telegram</item>
    ///   <item>... сервисы из BotEngine.Max</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddBotEngine(this IServiceCollection services)
    {
        services.AddScoped<ICommandFactory, CommandFactory>();

        // CommandDispatcher — Scoped: каждый входящий запрос обрабатывается в своём scope
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        // Для обратной совместимости с polling-воркерами, которые резолвят конкретный тип
        services.AddScoped<CommandDispatcher>(sp => (CommandDispatcher)sp.GetRequiredService<ICommandDispatcher>());

        // InMemoryUserSessionStore: Singleton (общий словарь) + HostedService (фоновая очистка)
        services.AddSingleton<InMemoryUserSessionStore>();
        services.AddSingleton<IUserSessionStore>(sp => sp.GetRequiredService<InMemoryUserSessionStore>());
        services.AddHostedService(sp => sp.GetRequiredService<InMemoryUserSessionStore>());

        // Здесь можно вызывать методы регистрации зависимостей из других модулей,
        // которые теперь являются частью этой же сборки.
        // Например:
        // services.AddTelegramServices();
        // services.AddMaxServices();

        return services;
    }
}
