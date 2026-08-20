using BotEngine.Core.Interfaces;
using MAX.Bot.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BotEngine.Max;

/// <summary>
/// Предоставляет методы расширения для интеграции платформы MAX в контейнер DI.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Регистрирует MAX-адаптер, HTTP-клиент, клиент MAX API и polling worker.
    /// Вызывается из Program.cs хост-приложения.
    /// </summary>
    /// <param name="services">Контейнер служб DI.</param>
    /// <returns>Контейнер служб с зарегистрированными зависимостями MAX.</returns>
    public static IServiceCollection AddMax(this IServiceCollection services)
    {
        services.AddSingleton<MaxPlatformAdapter>();
        services.AddSingleton<IMessagingPlatform>(sp => sp.GetRequiredService<MaxPlatformAdapter>());
        // Keyed DI: команды могут получить нужную платформу через IServiceProvider.GetKeyedService<IMessagingPlatform>("Max")
        services.AddKeyedSingleton<IMessagingPlatform>("Max", (sp, _) => sp.GetRequiredService<MaxPlatformAdapter>());

        services.AddHttpClient("MaxBot", (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var token = config["Max:Token"]
                ?? throw new InvalidOperationException("Max:Token не задан. Добавьте его в конфигурацию.");

            var timeoutSeconds = 30;
            if (int.TryParse(config["Max:TimeoutSeconds"], out var parsedTimeout))
            {
                timeoutSeconds = parsedTimeout;
            }

            client.BaseAddress = new Uri(MAX.Bot.MaxBotClient.BaseUrl);

            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Add("Authorization", token);
            }

            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        services.AddSingleton<MAX.Bot.Interfaces.IMaxBotClient>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var token = config["Max:Token"]
                ?? throw new InvalidOperationException("Max:Token не задан. Добавьте его в конфигурацию.");
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("MaxBot");
            return new MAX.Bot.MaxBotClient(token, httpClient);
        });

        services.AddHostedService<MaxPollingWorker>();

        return services;
    }

    /// <summary>
    /// Регистрирует MAX-адаптер, HTTP-клиент, клиент MAX API и polling worker с явно переданной конфигурацией.
    /// </summary>
    /// <param name="services">Контейнер служб DI.</param>
    /// <param name="config">Конфигурация приложения.</param>
    /// <returns>Контейнер служб с зарегистрированными зависимостями MAX.</returns>
    /// <exception cref="InvalidOperationException">Выбрасывается, если в конфигурации отсутствует токен Max:Token.</exception>
    public static IServiceCollection AddMaxPlatform(this IServiceCollection services, IConfiguration config)
    {
        var token = config["Max:Token"]
            ?? throw new InvalidOperationException("Max:Token не задан. Добавьте его в конфигурацию.");

        var timeoutSeconds = 30;
        if (int.TryParse(config["Max:TimeoutSeconds"], out var parsedTimeout))
        {
            timeoutSeconds = parsedTimeout;
        }

        services.AddSingleton<MaxPlatformAdapter>();
        services.AddSingleton<IMessagingPlatform>(sp => sp.GetRequiredService<MaxPlatformAdapter>());
        services.AddKeyedSingleton<IMessagingPlatform>("Max", (sp, _) => sp.GetRequiredService<MaxPlatformAdapter>());

        services.AddHttpClient("MaxBot", (sp, client) =>
        {
            client.BaseAddress = new Uri(MAX.Bot.MaxBotClient.BaseUrl);

            if (!string.IsNullOrWhiteSpace(token))
            {
                client.DefaultRequestHeaders.Add("Authorization", token);
            }

            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        services.AddSingleton<MAX.Bot.Interfaces.IMaxBotClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("MaxBot");
            return new MAX.Bot.MaxBotClient(token, httpClient);
        });

        services.AddHostedService<MaxPollingWorker>();

        return services;
    }
}
