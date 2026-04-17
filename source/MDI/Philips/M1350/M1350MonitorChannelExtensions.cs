using System.Threading.Channels;

namespace MDI.Philips.M1350;

/// <summary>
/// Provides channel-oriented adapters for <see cref="M1350Monitor" />.
/// </summary>
public static class M1350MonitorChannelExtensions
{
    /// <summary>
    /// Copies ordered monitor updates into a consumer-owned channel writer.
    /// </summary>
    /// <param name="monitor">The monitor that produces updates.</param>
    /// <param name="writer">The destination channel writer.</param>
    /// <param name="cancellationToken">The cancellation token for the copy operation.</param>
    /// <returns>A task that completes when the monitor update stream completes.</returns>
    /// <remarks>
    /// The caller still owns channel creation, buffering policy, and downstream consumption. This
    /// helper only bridges <see cref="M1350Monitor.WatchAsync(CancellationToken)" /> into an
    /// existing <see cref="ChannelWriter{T}" />.
    /// </remarks>
    public static async Task CopyUpdatesToAsync(
        this M1350Monitor monitor,
        ChannelWriter<M1350MonitorUpdate> writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        ArgumentNullException.ThrowIfNull(writer);

        Exception? completionError = null;

        try
        {
            await foreach (M1350MonitorUpdate update in monitor.WatchAsync(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteAsync(update, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            completionError = exception;
            throw;
        }
        finally
        {
            writer.TryComplete(completionError);
        }
    }
}
