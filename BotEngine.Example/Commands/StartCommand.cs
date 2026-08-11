using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Example.Commands;

/// <summary>
/// Команда /start: отправляет приветственное сообщение с Inline-клавиатурой.
/// </summary>
public class StartCommand : IBotCommand
{
    /// <inheritdoc />
    public string Name => "start";

    /// <inheritdoc />
    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct)
    {
        // Пример отправки клавиатуры (Inline) через фабричные методы BotButton
        var keyboard = BotKeyboard.Grid(new[]
        {
            new[] { BotButton.Callback("Пинг 🏓", "ping:"), BotButton.Callback("Эхо 🔁", "echo:") }
        });

        await context.ReplyAsync(
            "👋 Привет! Я тестовый бот на базе BotEngine.\n\nВыбери команду на клавиатуре или напиши /ping или /echo",
            keyboard: keyboard,
            ct: ct);
    }
}
