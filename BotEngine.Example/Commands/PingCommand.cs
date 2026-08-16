using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Example.Commands;

public sealed class PingCommand : IBotCommand
{
    public string Name => "ping";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        await context.ReplyAsync("Pong! 🏓", ct: ct);
    }
}
