using BotEngine.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;

namespace BotEngine.Telegram;

/// <summary>
/// Предоставляет методы расширения для интеграции платформы Telegram в контейнер DI.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Регистрирует Telegram-адаптер и polling worker, извлекая токен из конфигурации.
    /// Вызывается из Program.cs хост-приложения.
    /// </summary>
    /// <param name="services">Контейнер служб DI.</param>
    /// <returns>Контейнер служб с зарегистрированными зависимостями Telegram.</returns>
    public static IServiceCollection AddTelegram(this IServiceCollection services)
    {
        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var token = config["Telegram:Token"]
                ?? throw new InvalidOperationException("Telegram:Token не задан. Добавьте его в конфигурацию.");
            return new TelegramBotClient(token);
        });

        services.AddSingleton<TelegramPlatformAdapter>();
        services.AddSingleton<IMessagingPlatform>(sp => sp.GetRequiredService<TelegramPlatformAdapter>());
        services.AddHostedService<TelegramPollingWorker>();

        return services;
    }

    /// <summary>
    /// Регистрирует Telegram-адаптер и polling worker с явно заданным токеном.
    /// </summary>
    /// <param name="services">Контейнер служб DI.</param>
    /// <param name="token">Токен Telegram-бота.</param>
    /// <returns>Контейнер служб с зарегистрированными зависимостями Telegram.</returns>
    public static IServiceCollection AddTelegramPlatform(this IServiceCollection services, string token)
    {
        services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(token));

        services.AddSingleton<TelegramPlatformAdapter>();
        services.AddSingleton<IMessagingPlatform>(sp => sp.GetRequiredService<TelegramPlatformAdapter>());
        services.AddHostedService<TelegramPollingWorker>();

        return services;
    }
}
