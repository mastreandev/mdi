using System.Runtime.CompilerServices;
using System.Threading.Channels;

using MDI.Philips.M1350.Application.Identity;

namespace MDI.Philips.M1350;

/// <summary>
/// Coordinates a long-running monitor workflow over an <see cref="M1350Session" />.
/// </summary>
public sealed partial class M1350Monitor : IAsyncDisposable
{
    private readonly Lock gate = new();
    private readonly M1350Session session;
    private readonly List<Channel<M1350MonitorUpdate>> watchers = [];

    private CancellationTokenSource? runCancellationTokenSource;
    private Task? runTask;
    private Exception? terminalError;
    private M1350MonitorSnapshot snapshot = new();
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="M1350Monitor" /> class.
    /// </summary>
    /// <param name="session">The session used for transport and protocol coordination.</param>
    public M1350Monitor(M1350Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        this.session = session;
    }

    /// <summary>
    /// Gets the latest known monitor state.
    /// </summary>
    public M1350MonitorSnapshot Snapshot
    {
        get
        {
            lock (this.gate)
            {
                return this.snapshot;
            }
        }
    }

    /// <summary>
    /// Performs identity startup, optional revision negotiation, optional auto-send startup,
    /// and then begins the background receive loop.
    /// </summary>
    /// <remarks>
    /// This is the preferred entry point for normal monitor startup. Callers that subscribe with
    /// <see cref="WatchAsync(CancellationToken)" /> before invoking this method can observe the
    /// ordered startup-time state transitions published during startup, including identity,
    /// negotiated-revision, and auto-send activation updates.
    /// </remarks>
    public async ValueTask<M1350MonitorSnapshot> ConnectAsync(
        string? requestedRevision = null,
        AutoSendBehavior autoSend = AutoSendBehavior.Enabled,
        CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        this.ThrowIfStarted();

        IdBlock identity = await this.session.RequestIdentityAsync(cancellationToken).ConfigureAwait(false);
        this.ApplyIdentity(identity, publishUpdate: true);

        if (!string.IsNullOrEmpty(requestedRevision))
        {
            if (!M1350Session.IsProtocolRevisionSatisfied(identity, requestedRevision))
            {
                identity = await this.session.NegotiateRevisionAsync(requestedRevision, cancellationToken).ConfigureAwait(false);
                this.ApplyIdentity(identity, publishUpdate: true);
                this.ApplyNegotiatedRevision(identity.ProtocolRevision, publishUpdate: true);
            }
            else
            {
                this.ApplyNegotiatedRevision(identity.ProtocolRevision, publishUpdate: true);
            }
        }

        if (autoSend == AutoSendBehavior.Enabled)
        {
            await this.session.StartAutoSendAsync(cancellationToken).ConfigureAwait(false);
            this.ApplyAutoSendState(true, publishUpdate: true);
        }

        await this.StartAsync(cancellationToken).ConfigureAwait(false);
        return this.Snapshot;
    }

    /// <summary>
    /// Starts the background receive loop without performing any startup requests.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="ConnectAsync(string?, AutoSendBehavior, CancellationToken)" /> when the
    /// monitor should own startup policy. Use this method when identity requests, revision
    /// negotiation, or auto-send control have already been handled externally.
    /// </remarks>
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        this.ThrowIfStarted();
        cancellationToken.ThrowIfCancellationRequested();

        CancellationTokenSource runCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lock (this.gate)
        {
            this.runCancellationTokenSource = runCts;
            this.runTask = this.RunAsync(runCts.Token);
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Stops automatic CTG transmission when active and then stops the background receive loop.
    /// </summary>
    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        Task? runTask;
        CancellationTokenSource? runCts;
        bool autoSendActive;

        lock (this.gate)
        {
            runTask = this.runTask;
            runCts = this.runCancellationTokenSource;
            autoSendActive = this.snapshot.IsAutoSendActive;
        }

        if (runTask is null || runCts is null)
        {
            return;
        }

        if (autoSendActive)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.session.HaltAutoSendAsync(cancellationToken).ConfigureAwait(false);
            this.ApplyAutoSendState(false, publishUpdate: true);
        }

        await runCts.CancelAsync().ConfigureAwait(false);

        try
        {
            await runTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (runCts.IsCancellationRequested)
        {
        }
        finally
        {
            lock (this.gate)
            {
                if (ReferenceEquals(this.runCancellationTokenSource, runCts))
                {
                    this.runCancellationTokenSource = null;
                }
            }

            runCts.Dispose();
        }
    }

    /// <summary>
    /// Watches ordered monitor updates published by the background receive loop.
    /// </summary>
    /// <remarks>
    /// Callers may subscribe before <see cref="ConnectAsync(string?, AutoSendBehavior, CancellationToken)" />
    /// or <see cref="StartAsync(CancellationToken)" /> in order to observe ordered startup-time and
    /// monitor-owned state transitions. A watcher created after the monitor has already completed
    /// will observe immediate completion.
    /// </remarks>
    public async IAsyncEnumerable<M1350MonitorUpdate> WatchAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        Channel<M1350MonitorUpdate> channel = Channel.CreateUnbounded<M1350MonitorUpdate>(new UnboundedChannelOptions
        {
            SingleWriter = true,
            SingleReader = true,
        });

        lock (this.gate)
        {
            if (this.terminalError is not null)
            {
                channel.Writer.TryComplete(this.terminalError);
            }
            else if (this.runTask is { IsCompleted: true })
            {
                channel.Writer.TryComplete();
            }
            else
            {
                this.watchers.Add(channel);
            }
        }

        try
        {
            await foreach (M1350MonitorUpdate update in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return update;
            }
        }
        finally
        {
            lock (this.gate)
            {
                this.watchers.Remove(channel);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (this.isDisposed)
        {
            return;
        }

        try
        {
            await this.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            this.isDisposed = true;
            await this.session.DisposeAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }
    }
}
