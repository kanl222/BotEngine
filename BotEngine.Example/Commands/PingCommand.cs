using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Example.Commands;

/// <summary>
/// Команда /ping: отвечает «Pong! 🏓». Проверяет базовую связность бота.
/// </summary>
public sealed class PingCommand : IBotCommand
{
    /// <inheritdoc />
    public string Name => "ping";

    /// <inheritdoc />
    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        await context.ReplyAsync("Pong! 🏓", ct: ct);
    }
}
