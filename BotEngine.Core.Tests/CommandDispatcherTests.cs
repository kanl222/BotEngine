using BotEngine.Core.Configuration;
using BotEngine.Core.Interfaces;
using BotEngine.Core.Models;
using BotEngine.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BotEngine.Core.Tests;

public sealed class CommandDispatcherTests
{
    [Fact]
    public async Task ExplicitCommand_BypassesActiveSession()
    {
        var start = new RecordingCommand("start");
        var dialog = new RecordingCommand("echo");
        var factory = new DictionaryCommandFactory(start, dialog);
        var sessions = new RecordingSessionStore();
        var message = Message("/start");
        sessions.States[SessionKey(message)] = new UserDialogState("echo");

        await CreateDispatcher(factory, sessions).DispatchAsync(message, new RecordingPlatform());

        Assert.Equal(1, start.ExecutionCount);
        Assert.Equal(0, dialog.ExecutionCount);
    }

    [Fact]
    public async Task TelegramCommandSuffix_IsRemovedBeforeResolution()
    {
        var start = new RecordingCommand("start");
        var factory = new DictionaryCommandFactory(start);

        await CreateDispatcher(factory).DispatchAsync(
            Message("/START@ExampleBot argument"),
            new RecordingPlatform());

        Assert.Equal(1, start.ExecutionCount);
        Assert.Equal("start", factory.LastRequestedKey);
    }

    [Fact]
    public async Task Callback_HasPriorityOverExplicitTextAndSession()
    {
        var callback = new RecordingCommand("select");
        var start = new RecordingCommand("start");
        var dialog = new RecordingCommand("echo");
        var factory = new DictionaryCommandFactory(callback, start, dialog);
        var sessions = new RecordingSessionStore();
        var message = Message("/start") with { CallbackData = "select:42" };
        sessions.States[SessionKey(message)] = new UserDialogState("echo");

        await CreateDispatcher(factory, sessions).DispatchAsync(message, new RecordingPlatform());

        Assert.Equal(1, callback.ExecutionCount);
        Assert.Equal(0, start.ExecutionCount);
        Assert.Equal(0, dialog.ExecutionCount);
    }

    [Fact]
    public async Task UnknownSessionCommand_IsClearedBeforeFallback()
    {
        var fallback = new RecordingCommand("default");
        var factory = new DictionaryCommandFactory(fallback);
        var sessions = new RecordingSessionStore();
        var message = Message("обычный текст");
        var key = SessionKey(message);
        sessions.States[key] = new UserDialogState("removed-command");

        await CreateDispatcher(factory, sessions).DispatchAsync(message, new RecordingPlatform());

        Assert.DoesNotContain(key, sessions.States.Keys);
        Assert.Equal(1, fallback.ExecutionCount);
    }

    private static CommandDispatcher CreateDispatcher(
        ICommandFactory factory,
        IUserSessionStore? sessions = null) =>
        new(
            factory,
            sessions ?? new RecordingSessionStore(),
            SessionKeyProvider(),
            Array.Empty<IBotMiddleware>(),
            NullLogger<CommandDispatcher>.Instance);

    private static IncomingMessage Message(string text) =>
        new("chat-1", "user-1", text, null, "Telegram");

    private static ISessionKeyProvider SessionKeyProvider() =>
        new DefaultSessionKeyProvider(Options.Create(new BotEngineOptions { InstanceId = "tests" }));

    private static string SessionKey(IncomingMessage message) => SessionKeyProvider().Create(message);

    private sealed class DictionaryCommandFactory(params RecordingCommand[] commands) : ICommandFactory
    {
        private readonly IReadOnlyDictionary<string, IBotCommand> _commands =
            commands.ToDictionary(
                command => command.Name,
                command => (IBotCommand)command,
                StringComparer.OrdinalIgnoreCase);

        public string? LastRequestedKey { get; private set; }

        public IBotCommand? Resolve(string commandName)
        {
            LastRequestedKey = commandName;
            return _commands.GetValueOrDefault(commandName);
        }
    }

    private sealed class RecordingCommand(string name) : IBotCommand
    {
        public string Name { get; } = name;
        public int ExecutionCount { get; private set; }

        public Task ExecuteAsync(BotContext context, IncomingMessage message, CancellationToken ct = default)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSessionStore : IUserSessionStore
    {
        public Dictionary<string, UserDialogState> States { get; } = new(StringComparer.Ordinal);

        public Task<UserDialogState?> GetStateAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(States.TryGetValue(userId, out var state) ? (UserDialogState?)state : null);

        public Task SetStateAsync(
            string userId,
            UserDialogState state,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
        {
            States[userId] = state;
            return Task.CompletedTask;
        }

        public Task ClearStateAsync(string userId, CancellationToken ct = default)
        {
            States.Remove(userId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingPlatform : IMessagingPlatform
    {
        public event Func<IncomingMessage, Task>? OnMessageReceived
        {
            add { }
            remove { }
        }

        public Task SendTextAsync(
            string chatId,
            string text,
            BotKeyboard? keyboard = null,
            CancellationToken ct = default) => Task.CompletedTask;

        public Task SendLocationAsync(
            string chatId,
            double latitude,
            double longitude,
            CancellationToken ct = default) => Task.CompletedTask;

        public Task SendFileAsync(
            string chatId,
            Stream content,
            string fileName,
            string? mimeType = null,
            string? caption = null,
            BotKeyboard? keyboard = null,
            CancellationToken ct = default) => Task.CompletedTask;

        public Task<Stream> DownloadFileAsync(BotFile file, CancellationToken ct = default) =>
            Task.FromResult<Stream>(Stream.Null);
    }
}
