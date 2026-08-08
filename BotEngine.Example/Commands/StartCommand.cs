using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Example.Commands;

public class StartCommand : IBotCommand
{
    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct)
    {
        // Пример отправки клавиатуры (Inline)
        var keyboard = new BotKeyboard
        {
            Buttons = new List<List<BotButton>>
            {
                new()
                {
                    new BotButton { Text = "Пинг", CallbackData = "ping:" },
                    new BotButton { Text = "Эхо", CallbackData = "echo:" }
                }
            }
        };

        await context.MessagingPlatform.SendTextAsync(
            context.ChatId,
            "👋 Привет! Я тестовый бот на базе BotEngine.\n\nВыбери команду на клавиатуре или напиши /ping или /echo",
            keyboard: keyboard,
            ct: ct);
    }
}
