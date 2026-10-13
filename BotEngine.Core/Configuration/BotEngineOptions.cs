namespace BotEngine.Core.Configuration;

/// <summary>
/// Общие настройки экземпляра BotEngine.
/// </summary>
public sealed class BotEngineOptions
{
    public const string SectionName = "BotEngine";

    /// <summary>
    /// Стабильный идентификатор бота или арендатора. Участвует в формировании
    /// ключей сессий и изолирует несколько ботов, использующих одно хранилище.
    /// </summary>
    public string InstanceId { get; set; } = "default";
}
