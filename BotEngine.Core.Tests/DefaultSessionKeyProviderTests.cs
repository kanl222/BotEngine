using BotEngine.Core.Configuration;
using BotEngine.Core.Models;
using BotEngine.Core.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace BotEngine.Core.Tests;

public sealed class DefaultSessionKeyProviderTests
{
    [Fact]
    public void Create_IsolatesBotsPlatformsChatsAndUsers()
    {
        var firstBot = Provider("bot-a");
        var secondBot = Provider("bot-b");
        var baseline = Message("Telegram", "chat-a", "user-a");

        var keys = new[]
        {
            firstBot.Create(baseline),
            secondBot.Create(baseline),
            firstBot.Create(Message("Max", "chat-a", "user-a")),
            firstBot.Create(Message("Telegram", "chat-b", "user-a")),
            firstBot.Create(Message("Telegram", "chat-a", "user-b"))
        };

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Create_NormalizesPlatformButPreservesIdentifierCase()
    {
        var provider = Provider("Bot-A");

        var lowerPlatform = provider.Create(Message("telegram", "Chat", "User"));
        var upperPlatform = provider.Create(Message("TELEGRAM", "Chat", "User"));
        var differentUserCase = provider.Create(Message("telegram", "Chat", "user"));

        Assert.Equal(lowerPlatform, upperPlatform);
        Assert.NotEqual(lowerPlatform, differentUserCase);
    }

    private static DefaultSessionKeyProvider Provider(string instanceId) =>
        new(Options.Create(new BotEngineOptions { InstanceId = instanceId }));

    private static IncomingMessage Message(string platform, string chatId, string userId) =>
        new(chatId, userId, string.Empty, null, platform);
}
