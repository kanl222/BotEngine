namespace BotEngine.Core.Models;

/// <summary>
/// Представляет клавиатуру с набором кнопок для бота.
/// </summary>
/// <param name="Rows">Двумерный массив строк с кнопками.</param>
public record BotKeyboard(IReadOnlyList<IReadOnlyList<BotButton>> Rows)
{
    /// <summary>
    /// Создает одноколоночную клавиатуру из переданного списка кнопок.
    /// </summary>
    /// <param name="buttons">Набор кнопок, располагающихся вертикально.</param>
    /// <returns>Новый экземпляр <see cref="BotKeyboard"/>.</returns>
    public static BotKeyboard SingleColumn(params BotButton[] buttons)
        => new(buttons.Select(b => (IReadOnlyList<BotButton>)[b]).ToList());

    /// <summary>
    /// Создает клавиатуру в виде сетки.
    /// </summary>
    /// <param name="rows">Двумерная сетка кнопок.</param>
    /// <returns>Новый экземпляр <see cref="BotKeyboard"/>.</returns>
    public static BotKeyboard Grid(IEnumerable<IEnumerable<BotButton>> rows)
        => new(rows.Select(r => (IReadOnlyList<BotButton>)r.ToList()).ToList());
}
