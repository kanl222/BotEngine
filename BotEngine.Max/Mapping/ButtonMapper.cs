using BotEngine.Core.Models;
using MAX.Bot.Interfaces.Models.Request.Message.Attachment;
using MAX.Bot.Interfaces.Models.Request.Message.Attachment.Payloads;

namespace BotEngine.Max.Mapping;

/// <summary>
/// Класс-маппер для конвертации абстрактных клавиатур <see cref="BotKeyboard"/> 
/// в форматы вложений платформы MAX.
/// </summary>
internal static class ButtonMapper
{
    /// <summary>
    /// Создает инлайн-клавиатуру MAX на основе переданной клавиатуры бота.
    /// </summary>
    /// <param name="keyboard">Клавиатура бота.</param>
    /// <returns>Вложение в формате инлайн-клавиатуры MAX.</returns>
    public static InlineKeyboardAttachment ToInlineKeyboardAttachment(BotKeyboard keyboard)
    {
        var rows = keyboard.Rows
            .Select(row => row.Select(ToMaxButton).ToList())
            .ToList();

        return new InlineKeyboardAttachment
        {
            Payload = new InlineKeyboardPayload { Buttons = rows }
        };
    }

    /// <summary>
    /// Конвертирует абстрактную кнопку бота в кнопку платформы MAX.
    /// </summary>
    /// <param name="button">Кнопка бота.</param>
    /// <returns>Кнопка платформы MAX.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Выбрасывается при передаче неподдерживаемого или неизвестного типа кнопки.</exception>
    private static Button ToMaxButton(BotButton button) => button.Type switch
    {
        BotButtonType.Callback => new CallbackButton { Text = button.Text, Payload = button.Payload ?? button.Text },
        BotButtonType.Url => new LinkButton { Text = button.Text, Url = button.Url ?? string.Empty },
        BotButtonType.RequestGeo => new RequestGeoButton { Text = button.Text, Quick = true },
        BotButtonType.RequestText => new MessageButton { Text = button.Text },
        _ => throw new ArgumentOutOfRangeException(nameof(button.Type), $"Unsupported button type: {button.Type}")
    };
}
