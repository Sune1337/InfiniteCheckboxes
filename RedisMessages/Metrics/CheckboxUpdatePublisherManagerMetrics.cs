namespace RedisMessages.Metrics;

using System.Diagnostics.Metrics;

public static class CheckboxUpdatePublisherManagerMetrics
{
    #region Static Fields

    private static readonly Meter Meter = new("infinitecheckboxes_checkbox_update_publisher");
    public static readonly UpDownCounter<long> ActiveWorkers = Meter.CreateUpDownCounter<long>("worker_active_count");
    public static readonly Counter<long> MessageFailCount = Meter.CreateCounter<long>("message_fail_count");
    public static readonly Counter<long> MessagePublishCount = Meter.CreateCounter<long>("message_publish_count");
    public static readonly Histogram<double> MessagePublishLatency = Meter.CreateHistogram<double>("message_publish_latency");
    public static readonly UpDownCounter<long> QueuedMessageCount = Meter.CreateUpDownCounter<long>("messages_queue_length");

    #endregion
}
