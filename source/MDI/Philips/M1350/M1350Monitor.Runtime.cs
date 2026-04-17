using System.Threading.Channels;

using MDI.Philips.M1350.Application.Identity;

namespace MDI.Philips.M1350;

public sealed partial class M1350Monitor
{
    private static M1350MonitorUpdate CreateUpdate(M1350Message message, DateTimeOffset timestamp)
    {
        return message switch
        {
            IdMessage idMessage => new IdentityUpdated(idMessage.Block, timestamp),
            CtgMessage ctgMessage => new CtgUpdated(ctgMessage.Block, timestamp),
            NibpMessage nibpMessage => new NibpUpdated(nibpMessage.Block, timestamp),
            SpO2Message spO2Message => new SpO2Updated(spO2Message.Block, timestamp),
            TemperatureMessage temperatureMessage => new TemperatureUpdated(temperatureMessage.Block, timestamp),
            EventMarkerMessage eventMarkerMessage => new EventMarkerUpdated(eventMarkerMessage.Block, timestamp),
            NoteMessage noteMessage => new NoteUpdated(noteMessage.Block, timestamp),
            FailureMessage failureMessage => new FailureUpdated(failureMessage.Block, timestamp),
            _ => throw new InvalidOperationException($"Unsupported monitor message type '{message.GetType().Name}'."),
        };
    }

    private void ApplyAutoSendState(bool isAutoSendActive, bool publishUpdate)
    {
        AutoSendStateUpdated? update = null;

        lock (this.gate)
        {
            if (this.snapshot.IsAutoSendActive == isAutoSendActive)
            {
                return;
            }

            this.snapshot = this.snapshot with { IsAutoSendActive = isAutoSendActive };

            if (publishUpdate)
            {
                update = new AutoSendStateUpdated(isAutoSendActive, DateTimeOffset.UtcNow);
            }
        }

        if (update is not null)
        {
            this.PublishUpdate(update);
        }
    }

    private void ApplyIdentity(IdBlock block, bool publishUpdate)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        IdentityUpdated update;

        lock (this.gate)
        {
            this.snapshot = this.snapshot with
            {
                Identity = block,
                MessageReceivedAt = timestamp,
            };

            update = new IdentityUpdated(block, timestamp);
        }

        if (publishUpdate)
        {
            this.PublishUpdate(update);
        }
    }

    private void ApplyNegotiatedRevision(string negotiatedRevision, bool publishUpdate)
    {
        NegotiatedRevisionUpdated? update = null;

        lock (this.gate)
        {
            if (this.snapshot.NegotiatedRevision == negotiatedRevision)
            {
                return;
            }

            this.snapshot = this.snapshot with { NegotiatedRevision = negotiatedRevision };

            if (publishUpdate)
            {
                update = new NegotiatedRevisionUpdated(negotiatedRevision, DateTimeOffset.UtcNow);
            }
        }

        if (update is not null)
        {
            this.PublishUpdate(update);
        }
    }

    private void ApplyMessage(M1350Message message)
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;
        M1350MonitorUpdate update = CreateUpdate(message, timestamp);

        lock (this.gate)
        {
            this.snapshot = update switch
            {
                IdentityUpdated identityUpdated => this.snapshot with
                {
                    Identity = identityUpdated.Block,
                    MessageReceivedAt = timestamp,
                },
                CtgUpdated ctgUpdated => this.snapshot with
                {
                    Ctg = ctgUpdated.Block,
                    MessageReceivedAt = timestamp,
                    CtgReceivedAt = timestamp,
                },
                NibpUpdated nibpUpdated => this.snapshot with
                {
                    Nibp = nibpUpdated.Block,
                    MessageReceivedAt = timestamp,
                    NibpReceivedAt = timestamp,
                },
                SpO2Updated spO2Updated => this.snapshot with
                {
                    SpO2 = spO2Updated.Block,
                    MessageReceivedAt = timestamp,
                    SpO2ReceivedAt = timestamp,
                },
                TemperatureUpdated temperatureUpdated => this.snapshot with
                {
                    Temperature = temperatureUpdated.Block,
                    MessageReceivedAt = timestamp,
                    TemperatureReceivedAt = timestamp,
                },
                EventMarkerUpdated eventMarkerUpdated => this.snapshot with
                {
                    EventMarker = eventMarkerUpdated.Block,
                    MessageReceivedAt = timestamp,
                },
                NoteUpdated noteUpdated => this.snapshot with
                {
                    Note = noteUpdated.Block,
                    MessageReceivedAt = timestamp,
                },
                FailureUpdated failureUpdated => this.snapshot with
                {
                    Failure = failureUpdated.Block,
                    MessageReceivedAt = timestamp,
                    FailureReceivedAt = timestamp,
                },
                _ => this.snapshot,
            };
        }

        this.PublishUpdate(update);
    }

    private void CompleteWatchers(Exception? error = null)
    {
        List<Channel<M1350MonitorUpdate>> watchers;
        lock (this.gate)
        {
            watchers = [.. this.watchers];
            this.watchers.Clear();
        }

        foreach (Channel<M1350MonitorUpdate> watcher in watchers)
        {
            watcher.Writer.TryComplete(error);
        }
    }

    private void PublishUpdate(M1350MonitorUpdate update)
    {
        List<Channel<M1350MonitorUpdate>> watchers;
        lock (this.gate)
        {
            watchers = [.. this.watchers];
        }

        foreach (Channel<M1350MonitorUpdate> watcher in watchers)
        {
            watcher.Writer.TryWrite(update);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        Exception? terminalError = null;

        try
        {
            await foreach (M1350Message message in this.session.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                this.ApplyMessage(message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            terminalError = exception;
            throw;
        }
        finally
        {
            lock (this.gate)
            {
                this.terminalError = terminalError;
            }

            this.CompleteWatchers(terminalError);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(this.isDisposed, this);
    }

    private void ThrowIfStarted()
    {
        lock (this.gate)
        {
            if (this.runTask is not null)
            {
                throw new InvalidOperationException("This Philips M1350 monitor has already started its receive loop.");
            }
        }
    }
}
