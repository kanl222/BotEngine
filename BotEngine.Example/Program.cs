using BotEngine.Core;
using BotEngine.Core.Interfaces;
using BotEngine.Telegram;
using BotEngine.Max;
using BotEngine.Example.Commands;

var builder = Host.CreateApplicationBuilder(args);

// 1. Регистрируем ядро фреймворка (CommandDispatcher, Sessions и т.д.)
builder.Services.AddBotEngine();

// 2. Регистрируем платформы, которые хотим использовать.
// Для Telegram нужно добавить "Telegram:Token" в appsettings.json.
builder.Services.AddTelegram();
// builder.Services.AddMaxPlatform(); // Раскомментируйте, если есть токен для платформы Max

// 3. Регистрируем наши команды бота с помощью Keyed DI
builder.Services.AddKeyedScoped<IBotCommand, StartCommand>("start");
builder.Services.AddKeyedScoped<IBotCommand, PingCommand>("ping");
builder.Services.AddKeyedScoped<IBotCommand, EchoCommand>("echo");

var host = builder.Build();
host.Run();
