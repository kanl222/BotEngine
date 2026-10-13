using BotEngine.Core.Configuration;
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
using Microsoft.Extensions.Options;

namespace BotEngine.Core.Services;

/// <summary>
/// Изолирует сессии по экземпляру бота, платформе, чату и пользователю.
/// </summary>
public sealed class DefaultSessionKeyProvider : ISessionKeyProvider
{
    private const string SchemaVersion = "v2";
    private readonly string _instanceId;

    public DefaultSessionKeyProvider(IOptions<BotEngineOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _instanceId = RequireComponent(options.Value.InstanceId, nameof(BotEngineOptions.InstanceId));
    }

    /// <inheritdoc />
    public string Create(IncomingMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return string.Join(':',
            SchemaVersion,
            Escape(_instanceId),
            Escape(RequireComponent(message.Platform, nameof(message.Platform)).ToLowerInvariant()),
            Escape(RequireComponent(message.ChatId, nameof(message.ChatId))),
            Escape(RequireComponent(message.UserId, nameof(message.UserId))));
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static string RequireComponent(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        return value.Trim();
    }
}
