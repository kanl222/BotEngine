using System;
using System.Linq;
using BotEngine.Core.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace BotEngine.Telegram.Mapping;

/// <summary>
/// Класс-маппер для конвертации доменной абстрактной клавиатуры <see cref="BotKeyboard"/> 
/// в Telegram-специфичные разметки клавиатуры.
/// </summary>
internal static class ButtonMapper
{
    /// <summary>
    /// Создаёт <see cref="InlineKeyboardMarkup"/> из <see cref="BotKeyboard"/>.
    /// Поддерживает типы кнопок: <see cref="BotButtonType.Callback"/>, <see cref="BotButtonType.Url"/>.
    /// </summary>
    /// <param name="keyboard">Клавиатура бота.</param>
    /// <returns>Готовая инлайн-клавиатура для отправки в Telegram API.</returns>
    public static InlineKeyboardMarkup CreateInline(BotKeyboard keyboard)
    {
        var rows = keyboard.Rows.Select(row =>
            row.Select(MapInlineButton).ToArray()
        ).ToArray();

        return new InlineKeyboardMarkup(rows);
    }

    /// <summary>
    /// Создаёт <see cref="ReplyKeyboardMarkup"/> из <see cref="BotKeyboard"/>.
    /// Поддерживает типы кнопок: <see cref="BotButtonType.Callback"/>, 
    /// <see cref="BotButtonType.RequestGeo"/>, <see cref="BotButtonType.RequestText"/>.
    /// </summary>
    /// <param name="keyboard">Клавиатура бота.</param>
    /// <param name="resizeKeyboard">Флаг автоматической подстройки размера кнопок. По умолчанию true.</param>
    /// <param name="oneTimeKeyboard">Флаг одноразового отображения клавиатуры. По умолчанию false.</param>
    /// <returns>Обычная (reply) клавиатура для отправки в Telegram API.</returns>
    public static ReplyKeyboardMarkup CreateReply(BotKeyboard keyboard, bool resizeKeyboard = true, bool oneTimeKeyboard = false)
    {
        var rows = keyboard.Rows.Select(row =>
            row.Select(MapReplyButton).ToArray()
        ).ToArray();

        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = resizeKeyboard,
            OneTimeKeyboard = oneTimeKeyboard
        };
    }

    /// <summary>
    /// Конвертирует абстрактную кнопку в инлайн-кнопку Telegram.
    /// </summary>
    /// <param name="button">Кнопка бота.</param>
    /// <returns>Инлайн-кнопка Telegram.</returns>
    /// <exception cref="NotSupportedException">Выбрасывается при передаче неподдерживаемого типа кнопки.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Выбрасывается при передаче неизвестного типа кнопки.</exception>
    private static InlineKeyboardButton MapInlineButton(BotButton button) => button.Type switch
    {
        BotButtonType.Callback => InlineKeyboardButton.WithCallbackData(button.Text, button.Payload ?? string.Empty),
        BotButtonType.Url => InlineKeyboardButton.WithUrl(button.Text, button.Url ?? "#"),
        BotButtonType.RequestGeo => throw new NotSupportedException(
            "BotButtonType.RequestGeo не поддерживается в Telegram InlineKeyboardMarkup. " +
            "Используйте ButtonMapper.CreateReply() вместо inline-клавиатуры."),
        BotButtonType.RequestText => throw new NotSupportedException(
            "BotButtonType.RequestText не поддерживается в InlineKeyboardMarkup. " +
            "Используйте ButtonMapper.CreateReply() вместо inline-клавиатуры."),
        _ => throw new ArgumentOutOfRangeException(nameof(button.Type), button.Type, "Неизвестный тип кнопки.")
    };

    /// <summary>
    /// Конвертирует абстрактную кнопку в обычную (reply) кнопку Telegram.
    /// </summary>
    /// <param name="button">Кнопка бота.</param>
    /// <returns>Reply-кнопка Telegram.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Выбрасывается при передаче неизвестного типа кнопки.</exception>
    private static KeyboardButton MapReplyButton(BotButton button) => button.Type switch
    {
        BotButtonType.Callback or BotButtonType.RequestText => new KeyboardButton(button.Text),
        BotButtonType.RequestGeo => KeyboardButton.WithRequestLocation(button.Text),
        BotButtonType.Url => new KeyboardButton(button.Text), // URL не поддерживается в Reply — отображаем текст
        _ => throw new ArgumentOutOfRangeException(nameof(button.Type), button.Type, "Неизвестный тип кнопки.")
    };
}
