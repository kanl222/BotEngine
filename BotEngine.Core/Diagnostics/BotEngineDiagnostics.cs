using System.Diagnostics;
using System.Diagnostics.Metrics;
using BotEngine.Core.Models;

namespace BotEngine.Core.Diagnostics;

/// <summary>
/// Имена источников трассировки и метрик BotEngine для подключения OpenTelemetry.
/// </summary>
public static class BotEngineDiagnostics
{
    public const string ActivitySourceName = "BotEngine.Core";
    public const string MeterName = "BotEngine.Core";

    private static readonly ActivitySource Activities = new(ActivitySourceName);
    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Messages = Meter.CreateCounter<long>("botengine.messages.received");
    private static readonly Counter<long> Routes = Meter.CreateCounter<long>("botengine.messages.routed");
    private static readonly Counter<long> Failures = Meter.CreateCounter<long>("botengine.commands.failed");
    private static readonly Histogram<double> DispatchDuration = Meter.CreateHistogram<double>(
        "botengine.dispatch.duration",
        unit: "ms");

    internal static Activity? StartDispatch(IncomingMessage message)
    {
        Messages.Add(1, new KeyValuePair<string, object?>("messaging.system", message.Platform));

        var activity = Activities.StartActivity("botengine.dispatch", ActivityKind.Consumer);
        activity?.SetTag("messaging.system", message.Platform);
        activity?.SetTag("messaging.message.id", message.CorrelationId ?? message.MessageId);
        activity?.SetTag("botengine.has_callback", !string.IsNullOrEmpty(message.CallbackData));
        return activity;
    }

    internal static void RecordRoute(string platform, string route)
    {
        Activity.Current?.SetTag("botengine.route", route);
        Routes.Add(1,
            new KeyValuePair<string, object?>("messaging.system", platform),
            new KeyValuePair<string, object?>("botengine.route", route));
    }

    internal static void RecordCommandFailure(string platform, string commandName, Exception exception)
    {
        Activity.Current?.SetStatus(ActivityStatusCode.Error, exception.Message);
        Activity.Current?.AddException(exception);
        Failures.Add(1,
            new KeyValuePair<string, object?>("messaging.system", platform),
            new KeyValuePair<string, object?>("botengine.command", commandName));
    }

    internal static void RecordDispatchDuration(string platform, TimeSpan duration) =>
        DispatchDuration.Record(duration.TotalMilliseconds,
            new KeyValuePair<string, object?>("messaging.system", platform));
}
