using System.Reflection;
using BotEngine.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace BotEngine.Core.Tests;

public sealed class RedisUserSessionStoreTests
{
    [Fact]
    public async Task GetStateAsync_RethrowsRedisFailure()
    {
        var database = Proxy<IDatabase>((method, _) =>
            method.Name == nameof(IDatabase.StringGetAsync)
                ? throw new InvalidOperationException("redis unavailable")
                : throw Unexpected(method));
        var store = CreateStore(database);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.GetStateAsync("session"));

        Assert.Equal("redis unavailable", exception.Message);
    }

    [Fact]
    public async Task GetStateAsync_UsesVersionedRedisPrefix()
    {
        RedisKey capturedKey = default;
        var database = Proxy<IDatabase>((method, arguments) =>
        {
            if (method.Name != nameof(IDatabase.StringGetAsync))
                throw Unexpected(method);

            capturedKey = (RedisKey)arguments![0]!;
            return Task.FromResult(RedisValue.Null);
        });
        var store = CreateStore(database);

        var state = await store.GetStateAsync("v2:bot:telegram:chat:user");

        Assert.Null(state);
        Assert.Equal("bot:session:v2:bot:telegram:chat:user", capturedKey.ToString());
    }

    private static RedisUserSessionStore CreateStore(IDatabase database)
    {
        var connection = Proxy<IConnectionMultiplexer>((method, _) =>
            method.Name == nameof(IConnectionMultiplexer.GetDatabase)
                ? database
                : throw Unexpected(method));

        return new RedisUserSessionStore(
            connection,
            NullLogger<RedisUserSessionStore>.Instance);
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, DelegateDispatchProxy>();
        ((DelegateDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    private static Exception Unexpected(MethodInfo method) =>
        new NotSupportedException($"Unexpected proxy call: {method.Name}");
}

public class DelegateDispatchProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        Handler(targetMethod ?? throw new InvalidOperationException("Proxy method is missing."), args);
}
