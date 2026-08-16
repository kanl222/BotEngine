using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Example.Commands;

public sealed class EchoCommand : IBotCommand
{
    public string Name => "echo";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        // Проверяем, есть ли уже активная сессия для этого пользователя
        var state = await context.GetSessionAsync(ct);

        if (state is null)
        {
            // Шаг 1: Запрашиваем ввод и устанавливаем состояние
            await context.ReplyAsync("Что мне тебе ответить? Напиши любое сообщение:", ct: ct);
            await context.SetSessionAsync("echo", ct: ct);
        }
        else
        {
            // Шаг 2: Обрабатываем полученный ввод и очищаем состояние
            var text = string.IsNullOrWhiteSpace(message.Text) ? "[пустое сообщение]" : message.Text;

            await context.ReplyAsync($"Ты сказал: {text}", ct: ct);

            // Обязательно очищаем сессию, чтобы выйти из диалога
            await context.ClearSessionAsync(ct);
        }
    }
}
