namespace BotEngine.Core.Models;

/// <summary>
/// Определяет тип кнопки бота.
/// </summary>
public enum BotButtonType
{
    /// <summary>
    /// Callback-кнопка для отправки скрытых данных (инлайн-режим).
    /// </summary>
    Callback,

    /// <summary>
    /// Кнопка со ссылкой на внешний веб-ресурс.
    /// </summary>
    Url,

    /// <summary>
    /// Кнопка для отправки текущего местоположения (геопозиции) пользователя.
    /// </summary>
    RequestGeo,

    /// <summary>
    /// Кнопка запроса текстового ввода.
    /// </summary>
    RequestText
}

/// <summary>
/// Представляет кнопку на клавиатуре бота.
/// </summary>
public readonly record struct BotButton
{
    /// <summary>
    /// Возвращает текст на кнопке.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Возвращает тип кнопки.
    /// </summary>
    public BotButtonType Type { get; init; }

    /// <summary>
    /// Возвращает callback-данные, передаваемые при нажатии кнопки.
    /// </summary>
    public string? Payload { get; init; }

    /// <summary>
    /// Возвращает URL-адрес для кнопок-ссылок.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BotButton"/>.
    /// </summary>
    /// <param name="text">Текст на кнопке.</param>
    /// <param name="type">Тип кнопки.</param>
    /// <param name="payload">Параметры callback-запроса.</param>
    /// <param name="url">Целевой URL-адрес.</param>
    private BotButton(string text, BotButtonType type, string? payload = null, string? url = null)
    {
        Text = text;
        Type = type;
        Payload = payload;
        Url = url;
    }

    /// <summary>
    /// Создает callback-кнопку для инлайн-клавиатуры.
    /// </summary>
    /// <param name="text">Текст на кнопке.</param>
    /// <param name="payload">Данные, отправляемые обратно в обработчик команды.</param>
    /// <returns>Экземпляр callback-кнопки <see cref="BotButton"/>.</returns>
    public static BotButton Callback(string text, string payload)
        => new(text, BotButtonType.Callback, payload: payload);

    /// <summary>
    /// Создает кнопку-ссылку на внешний ресурс.
    /// </summary>
    /// <param name="text">Текст на кнопке.</param>
    /// <param name="url">Целевой URL-адрес.</param>
    /// <returns>Экземпляр URL-кнопки <see cref="BotButton"/>.</returns>
    public static BotButton Link(string text, string url)
        => new(text, BotButtonType.Url, url: url);

    /// <summary>
    /// Создает кнопку для запроса геопозиции пользователя.
    /// </summary>
    /// <param name="text">Текст на кнопке.</param>
    /// <returns>Экземпляр кнопки запроса геопозиции <see cref="BotButton"/>.</returns>
    public static BotButton RequestGeo(string text)
        => new(text, BotButtonType.RequestGeo);

    /// <summary>
    /// Создает кнопку для запроса ввода текста.
    /// </summary>
    /// <param name="text">Текст на кнопке.</param>
    /// <returns>Экземпляр кнопки запроса текста <see cref="BotButton"/>.</returns>
    public static BotButton RequestText(string text)
        => new(text, BotButtonType.RequestText);
}
