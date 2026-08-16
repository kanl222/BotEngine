using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Example.Commands;

public sealed class StartCommand : IBotCommand
{
    public string Name => "start";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        // Пример отправки клавиатуры (Inline)
        var keyboard = BotKeyboard.SingleColumn(
            BotButton.Callback("🏓 Пинг", "ping:"),
            BotButton.Callback("🗣️ Эхо", "echo:")
        );

        await context.ReplyAsync(
            "👋 Привет! Я тестовый бот на базе BotEngine.\n\nВыбери команду на клавиатуре или напиши /ping или /echo",
            keyboard: keyboard,
            ct: ct);
    }
}
