using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;

namespace BotEngine.Core.Commands;

/// <summary>
/// Единая команда приветствия для Telegram и MAX мессенджеров.
/// </summary>
public sealed class StartCommand : IBotCommand
{
    public string Name => "start";

    public async Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
    {
        var welcomeText = 
            "Здравствуйте! Я ваш корпоративный интеллектуальный ассистент по базе знаний **Confluence**. 🤖\n\n" +
            "Вместо простой выдачи списка ссылок я прочитываю регламенты компании в Confluence, анализирую информацию и генерирую точный ответ на ваш вопрос естественным языком, прикрепляя прямые ссылки на первоисточники.\n\n" +
            "Вы можете задать любой вопрос естественным языком или выберите тему ниже:";

        var keyboard = new BotKeyboard(new[]
        {
            new[] { BotButton.Callback("🌴 Оформление отпуска", "ask:Как оформить отпуск?") },
            new[] { BotButton.Callback("🔒 Настройка VPN", "ask:Как настроить корпоративный VPN?") },
            new[] { BotButton.Callback("💻 Заказ техники", "ask:Как заказать рабочий ноутбук?") },
            new[] { BotButton.Callback("💰 Оплата больничного", "ask:Как оплачивается больничный лист?") }
        });

        await context.ReplyAsync(welcomeText, keyboard);
    }
}
