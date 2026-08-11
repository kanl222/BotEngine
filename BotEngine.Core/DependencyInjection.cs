using BotEngine.Core.Interfaces;
using BotEngine.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace BotEngine.Core;

/// <summary>
/// Расширения DI-контейнера для регистрации ядра BotEngine.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Регистрирует все базовые сервисы BotEngine:
    /// <list type="bullet">
    ///   <item><see cref="ICommandFactory"/> → <see cref="CommandFactory"/> (Singleton)</item>
    ///   <item><see cref="ICommandDispatcher"/> → <see cref="CommandDispatcher"/> (Scoped)</item>
    ///   <item><see cref="IUserSessionStore"/> → <see cref="InMemoryUserSessionStore"/> (Singleton + HostedService)</item>
    /// </list>
    /// Для горизонтального масштабирования замените InMemory на Redis,
    /// вызвав <see cref="AddRedisSessionStore"/> после этого метода.
    /// </summary>
    public static IServiceCollection AddBotEngine(this IServiceCollection services)
    {
        services.AddSingleton<ICommandFactory, CommandFactory>();

        // CommandDispatcher — Scoped: каждый входящий запрос обрабатывается в своём scope
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        // Для обратной совместимости с polling-воркерами, которые резолвят конкретный тип
        services.AddScoped<CommandDispatcher>(sp => (CommandDispatcher)sp.GetRequiredService<ICommandDispatcher>());

        // InMemoryUserSessionStore: Singleton (общий словарь) + HostedService (фоновая очистка)
        services.AddSingleton<InMemoryUserSessionStore>();
        services.AddSingleton<IUserSessionStore>(sp => sp.GetRequiredService<InMemoryUserSessionStore>());
        services.AddHostedService(sp => sp.GetRequiredService<InMemoryUserSessionStore>());

        return services;
    }

    /// <summary>
    /// Заменяет InMemory-хранилище сессий на Redis-реализацию.
    /// Вызывать <strong>после</strong> <see cref="AddBotEngine"/>.
    /// </summary>
    /// <param name="services">Контейнер служб.</param>
    /// <param name="connectionString">Строка подключения Redis (например, <c>redis:6379</c>).</param>
    /// <returns>Контейнер служб с зарегистрированным Redis-хранилищем.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddBotEngine();
    /// builder.Services.AddRedisSessionStore(builder.Configuration["REDIS_CONNECTION_STRING"]!);
    /// </code>
    /// </example>
    public static IServiceCollection AddRedisSessionStore(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Перерегистрируем IUserSessionStore → Redis
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        services.AddSingleton<IUserSessionStore, RedisUserSessionStore>();

        return services;
    }

    /// <summary>
    /// Регистрирует Rate Limiting Middleware с настройками по умолчанию.
    /// </summary>
    /// <param name="services">Контейнер служб.</param>
    /// <param name="configure">Необязательный делегат конфигурации.</param>
    /// <returns>Контейнер служб с зарегистрированным Rate Limiting.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddBotEngine();
    /// builder.Services.AddRateLimiting(opts =>
    /// {
    ///     opts.MaxMessages = 20;
    ///     opts.WindowSeconds = 60;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        Action<RateLimitOptions>? configure = null)
    {
        var optionsBuilder = services.AddOptions<RateLimitOptions>()
            .BindConfiguration(RateLimitOptions.SectionName);

        if (configure is not null)
            optionsBuilder.Configure(configure);

        services.AddSingleton<IBotMiddleware, RateLimitMiddleware>();
        return services;
    }
}
