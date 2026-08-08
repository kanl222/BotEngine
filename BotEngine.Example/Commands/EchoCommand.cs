using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Example.Commands;

public class EchoCommand : IBotCommand
{
    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct)
    {
        // Проверяем, есть ли уже активная сессия для этого пользователя
        var state = await context.Sessions.GetStateAsync(context.UserId, ct);
        
        if (state == null)
        {
            // Шаг 1: Запрашиваем ввод и устанавливаем состояние
            await context.MessagingPlatform.SendTextAsync(
                context.ChatId, 
                "Что мне тебе ответить? Напиши любое сообщение:", 
                ct: ct);

            await context.Sessions.SetStateAsync(context.UserId, new UserDialogState
            {
                UserId = context.UserId,
                AwaitingInputFor = "echo", // Указываем ключ команды, которая будет обрабатывать следующий ввод
                Data = new Dictionary<string, string>()
            }, ct);
        }
        else
        {
            // Шаг 2: Обрабатываем полученный ввод и очищаем состояние
            var text = message.Text ?? "[пустое сообщение]";
            
            await context.MessagingPlatform.SendTextAsync(
                context.ChatId, 
                $"Ты сказал: {text}", 
                ct: ct);

            // Обязательно очищаем сессию, чтобы выйти из диалога
            await context.Sessions.ClearStateAsync(context.UserId, ct);
        }
    }
}
