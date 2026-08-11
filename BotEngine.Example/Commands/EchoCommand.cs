using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Example.Commands;

/// <summary>
/// Команда /echo: демонстрирует двухшаговый диалог с сохранением состояния через <see cref="UserDialogState"/>.
/// Шаг 1: запрашивает текст у пользователя.
/// Шаг 2: повторяет введённый текст и завершает диалог.
/// </summary>
public class EchoCommand : IBotCommand
{
    /// <inheritdoc />
    public string Name => "echo";

    /// <inheritdoc />
    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct)
    {
        // Проверяем, есть ли уже активная сессия для этого пользователя
        var state = await context.GetSessionAsync(ct);

        if (state == null)
        {
            // Шаг 1: запрашиваем ввод и сохраняем состояние
            await context.ReplyAsync("Что мне тебе ответить? Напиши любое сообщение:", ct: ct);

            // Указываем ключ команды, которая будет обрабатывать следующий ввод
            await context.SetSessionAsync(Name, ct: ct);
        }
        else
        {
            // Шаг 2: обрабатываем полученный ввод и очищаем состояние
            var text = message.Text ?? "[пустое сообщение]";

            await context.ReplyAsync($"Ты сказал: {text}", ct: ct);

            // Обязательно очищаем сессию, чтобы выйти из диалога
            await context.ClearSessionAsync(ct);
        }
    }
}
