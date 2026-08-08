using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Example.Commands;

public class PingCommand : IBotCommand
{
    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct)
    {
        // Простая команда, возвращающая ответ
        await context.MessagingPlatform.SendTextAsync(context.ChatId, "Pong! 🏓", ct: ct);
    }
}
