using System.Buffers;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace MDI.Philips.M1350;

public sealed partial class M1350Session
{
    /// <summary>
    /// Reads framed input until cancellation or input completion, yielding supported parsed messages.
    /// </summary>
    public async IAsyncEnumerable<M1350Message> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.EnterAsyncReadScope();
        long startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            while (await this.TryReadNextAsync(cancellationToken).ConfigureAwait(false) is { } message)
            {
                yield return message.WithObservation(
                    Stopwatch.GetElapsedTime(startTimestamp),
                    M1350MessageDirection.Inbound);
            }
        }
        finally
        {
            this.ExitAsyncReadScope();
        }
    }

    private void EnterAsyncReadScope()
    {
        this.EnsureAsyncInput();

        if (Interlocked.CompareExchange(ref this.asyncReadInUse, 1, 0) != 0)
        {
            throw new InvalidOperationException("Concurrent asynchronous reads are not supported on a single Philips M1350 session.");
        }
    }

    private void ExitAsyncReadScope()
    {
        Volatile.Write(ref this.asyncReadInUse, 0);
    }

    private PipeReader EnsureAsyncInput()
    {
        return this.input ?? throw new InvalidOperationException(
            "This Philips M1350 session was created without an asynchronous PipeReader input.");
    }

    private async ValueTask FlushOutputAsync(CancellationToken cancellationToken)
    {
        if (this.output is PipeWriter pipeWriter)
        {
            FlushResult result = await pipeWriter.FlushAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsCanceled)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private async ValueTask<M1350Message?> TryReadNextAsync(CancellationToken cancellationToken)
    {
        PipeReader input = this.EnsureAsyncInput();

        while (true)
        {
            ReadResult result = await input.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = result.Buffer;
            ReadOnlySequence<byte> remainder = buffer;

            if (M1350MessageReader.TryRead(ref remainder, out M1350Message message))
            {
                input.AdvanceTo(remainder.Start, remainder.Start);
                return message;
            }

            if (result.IsCompleted)
            {
                input.AdvanceTo(buffer.End, buffer.End);
                return null;
            }

            input.AdvanceTo(remainder.Start, buffer.End);
        }
    }
}
