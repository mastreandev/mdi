using System.Buffers;

using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Identity;

namespace MDI.Philips.M1350;

public sealed partial class M1350Session
{
    /// <summary>
    /// Writes a framed request for the monitor identity block (<c>?I</c>).
    /// </summary>
    public void RequestIdentity()
    {
        M1350CommandWriter.WriteRequestIdentity(this.output);
    }

    /// <summary>
    /// Writes <c>?I</c> and attempts to read the next identity block from the framed input buffer.
    /// </summary>
    public bool TryRequestIdentity(ref ReadOnlySequence<byte> buffer, out IdBlock block)
    {
        this.RequestIdentity();
        return TryReadIdentity(ref buffer, out block);
    }

    /// <summary>
    /// Writes <c>?I</c>, flushes the outbound command, and waits for the next identity block.
    /// </summary>
    public async ValueTask<IdBlock> RequestIdentityAsync(CancellationToken cancellationToken = default)
    {
        this.EnterAsyncReadScope();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            this.RequestIdentity();
            await this.FlushOutputAsync(cancellationToken).ConfigureAwait(false);
            return await this.ReadIdentityAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.ExitAsyncReadScope();
        }
    }

    /// <summary>
    /// Writes a framed request for the CTG block (<c>?C</c>).
    /// </summary>
    public void RequestCtg()
    {
        M1350CommandWriter.WriteRequestCtg(this.output);
    }

    /// <summary>
    /// Writes <c>?C</c> and attempts to read the next CTG block from the framed input buffer.
    /// </summary>
    public bool TryRequestCtg(ref ReadOnlySequence<byte> buffer, out CtgBlock block)
    {
        this.RequestCtg();
        return TryReadCtg(ref buffer, out block);
    }

    /// <summary>
    /// Writes <c>?C</c>, flushes the outbound command, and waits for the next CTG block.
    /// </summary>
    public async ValueTask<CtgBlock> RequestCtgAsync(CancellationToken cancellationToken = default)
    {
        this.EnterAsyncReadScope();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            this.RequestCtg();
            await this.FlushOutputAsync(cancellationToken).ConfigureAwait(false);
            return await this.ReadCtgAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.ExitAsyncReadScope();
        }
    }

    /// <summary>
    /// Writes a framed command that starts automatic CTG transmission (<c>G</c>).
    /// </summary>
    public void StartAutoSend()
    {
        M1350CommandWriter.WriteStartAutoSend(this.output);
    }

    /// <summary>
    /// Writes a framed command that starts automatic CTG transmission (<c>G</c>)
    /// and flushes the output when supported.
    /// </summary>
    public async ValueTask StartAutoSendAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        this.StartAutoSend();
        await this.FlushOutputAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a framed command that halts automatic CTG transmission (<c>H</c>).
    /// </summary>
    public void HaltAutoSend()
    {
        M1350CommandWriter.WriteHaltAutoSend(this.output);
    }

    /// <summary>
    /// Writes a framed command that halts automatic CTG transmission (<c>H</c>)
    /// and flushes the output when supported.
    /// </summary>
    public async ValueTask HaltAutoSendAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        this.HaltAutoSend();
        await this.FlushOutputAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a framed note block with an optional user identifier.
    /// </summary>
    public void SendNote(string text, string userId)
    {
        M1350CommandWriter.WriteNote(this.output, text, userId);
    }

    /// <summary>
    /// Writes a framed note block without a user identifier.
    /// </summary>
    public void SendNote(string text)
    {
        M1350CommandWriter.WriteNote(this.output, text);
    }

    /// <summary>
    /// Writes a framed note block with an optional user identifier and flushes the output when supported.
    /// </summary>
    public async ValueTask SendNoteAsync(
        string text,
        string userId = "",
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        this.SendNote(text, userId);
        await this.FlushOutputAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a framed protocol revision change request (<c>Vxxx</c>).
    /// </summary>
    /// <param name="requestedRevision">The 3-character revision token, for example <c>A20</c>.</param>
    public void RequestProtocolRevisionChange(string requestedRevision)
    {
        M1350CommandWriter.WriteProtocolRevisionChange(this.output, requestedRevision);
    }

    /// <summary>
    /// Begins revision negotiation by writing <c>Vxxx</c> followed by <c>?I</c>.
    /// </summary>
    /// <param name="requestedRevision">The 3-character revision token, for example <c>A20</c>.</param>
    public void NegotiateProtocolRevision(string requestedRevision)
    {
        this.RequestProtocolRevisionChange(requestedRevision);
        this.RequestIdentity();
    }

    /// <summary>
    /// Writes <c>Vxxx</c> followed by <c>?I</c> and attempts to read a negotiated identity block
    /// whose protocol revision satisfies <paramref name="requestedRevision" />.
    /// </summary>
    public bool TryNegotiateProtocolRevision(
        ref ReadOnlySequence<byte> buffer,
        string requestedRevision,
        out IdBlock block)
    {
        this.NegotiateProtocolRevision(requestedRevision);
        return TryReadNegotiatedIdentity(ref buffer, requestedRevision, out block);
    }

    /// <summary>
    /// Writes <c>Vxxx</c> followed by <c>?I</c>, flushes the outbound commands, and waits for
    /// an identity block whose revision satisfies <paramref name="requestedRevision" />.
    /// </summary>
    public async ValueTask<IdBlock> NegotiateRevisionAsync(
        string requestedRevision,
        CancellationToken cancellationToken = default)
    {
        this.EnterAsyncReadScope();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            this.NegotiateProtocolRevision(requestedRevision);
            await this.FlushOutputAsync(cancellationToken).ConfigureAwait(false);
            return await this.ReadNegotiatedIdentityAsync(requestedRevision, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.ExitAsyncReadScope();
        }
    }
}
