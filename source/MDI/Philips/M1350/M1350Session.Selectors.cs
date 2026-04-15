using System.Buffers;

using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.EventMessage;
using MDI.Philips.M1350.Application.Failure;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Nibp;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.Application.SpO2;
using MDI.Philips.M1350.Application.Temperature;

namespace MDI.Philips.M1350;

public sealed partial class M1350Session
{
    /// <summary>
    /// Determines whether an identity block reports a protocol revision that satisfies
    /// the requested revision.
    /// </summary>
    public static bool IsProtocolRevisionSatisfied(in IdBlock block, string requestedRevision)
    {
        ArgumentNullException.ThrowIfNull(requestedRevision);

        if (!ProtocolRevision.TryParse(block.ProtocolRevision.AsSpan(), out ProtocolRevision actual))
        {
            throw new ArgumentException(
                "The identity block contains an invalid 3-character protocol revision token.",
                nameof(block));
        }

        if (!ProtocolRevision.TryParse(requestedRevision.AsSpan(), out ProtocolRevision requested))
        {
            throw new ArgumentException(
                "Protocol revision must be a 3-character ASCII token, for example A20.",
                nameof(requestedRevision));
        }

        return actual.CompareTo(requested) >= 0;
    }

    /// <summary>
    /// Attempts to read the next supported message from the framed input buffer.
    /// </summary>
    public static bool TryRead(ref ReadOnlySequence<byte> buffer, out M1350Message message)
    {
        return M1350MessageReader.TryRead(ref buffer, out message);
    }

    /// <summary>
    /// Attempts to read the next identity block from the framed input buffer,
    /// skipping other supported message types.
    /// </summary>
    public static bool TryReadIdentity(ref ReadOnlySequence<byte> buffer, out IdBlock block)
    {
        while (M1350MessageReader.TryRead(ref buffer, out M1350Message message))
        {
            if (message is IdMessage idMessage)
            {
                block = idMessage.Block;
                return true;
            }
        }

        block = default;
        return false;
    }

    /// <summary>
    /// Attempts to read the next identity block from the framed input buffer and validate
    /// that its protocol revision satisfies the requested revision.
    /// </summary>
    public static bool TryReadNegotiatedIdentity(
        ref ReadOnlySequence<byte> buffer,
        string requestedRevision,
        out IdBlock block)
    {
        while (TryReadIdentity(ref buffer, out IdBlock candidate))
        {
            if (IsProtocolRevisionSatisfied(candidate, requestedRevision))
            {
                block = candidate;
                return true;
            }

            block = default;
            return false;
        }

        block = default;
        return false;
    }

    /// <summary>
    /// Attempts to read the next CTG block from the framed input buffer,
    /// skipping other supported message types.
    /// </summary>
    public static bool TryReadCtg(ref ReadOnlySequence<byte> buffer, out CtgBlock block)
    {
        while (M1350MessageReader.TryRead(ref buffer, out M1350Message message))
        {
            if (message is CtgMessage ctgMessage)
            {
                block = ctgMessage.Block;
                return true;
            }
        }

        block = default;
        return false;
    }

    /// <summary>
    /// Attempts to read the next event marker block from the framed input buffer,
    /// skipping other supported message types.
    /// </summary>
    public static bool TryReadEventMessage(ref ReadOnlySequence<byte> buffer, out EventMessageBlock block)
    {
        while (M1350MessageReader.TryRead(ref buffer, out M1350Message message))
        {
            if (message is EventMarkerMessage eventMessage)
            {
                block = eventMessage.Block;
                return true;
            }
        }

        block = default;
        return false;
    }

    /// <summary>
    /// Attempts to read the next note block from the framed input buffer,
    /// skipping other supported message types.
    /// </summary>
    public static bool TryReadNote(ref ReadOnlySequence<byte> buffer, out NoteBlock block)
    {
        while (M1350MessageReader.TryRead(ref buffer, out M1350Message message))
        {
            if (message is NoteMessage noteMessage)
            {
                block = noteMessage.Block;
                return true;
            }
        }

        block = default;
        return false;
    }

    /// <summary>
    /// Attempts to read the next failure block from the framed input buffer,
    /// skipping other supported message types.
    /// </summary>
    public static bool TryReadFailure(ref ReadOnlySequence<byte> buffer, out FailureBlock block)
    {
        while (M1350MessageReader.TryRead(ref buffer, out M1350Message message))
        {
            if (message is FailureMessage failureMessage)
            {
                block = failureMessage.Block;
                return true;
            }
        }

        block = default;
        return false;
    }

    /// <summary>
    /// Attempts to read the next maternal blood-pressure block from the framed input buffer,
    /// skipping other supported message types.
    /// </summary>
    public static bool TryReadNibp(ref ReadOnlySequence<byte> buffer, out NibpBlock block)
    {
        while (M1350MessageReader.TryRead(ref buffer, out M1350Message message))
        {
            if (message is NibpMessage nibpMessage)
            {
                block = nibpMessage.Block;
                return true;
            }
        }

        block = default;
        return false;
    }

    /// <summary>
    /// Attempts to read the next maternal temperature block from the framed input buffer,
    /// skipping other supported message types.
    /// </summary>
    public static bool TryReadTemperature(ref ReadOnlySequence<byte> buffer, out TemperatureBlock block)
    {
        while (M1350MessageReader.TryRead(ref buffer, out M1350Message message))
        {
            if (message is TemperatureMessage temperatureMessage)
            {
                block = temperatureMessage.Block;
                return true;
            }
        }

        block = default;
        return false;
    }

    /// <summary>
    /// Attempts to read the next maternal oxygen saturation block from the framed input buffer,
    /// skipping other supported message types.
    /// </summary>
    public static bool TryReadSpO2(ref ReadOnlySequence<byte> buffer, out SpO2Block block)
    {
        while (M1350MessageReader.TryRead(ref buffer, out M1350Message message))
        {
            if (message is SpO2Message spO2Message)
            {
                block = spO2Message.Block;
                return true;
            }
        }

        block = default;
        return false;
    }

    private async ValueTask<IdBlock> ReadIdentityAsync(CancellationToken cancellationToken)
    {
        while (await this.TryReadNextAsync(cancellationToken).ConfigureAwait(false) is { } message)
        {
            if (message is IdMessage idMessage)
            {
                return idMessage.Block;
            }
        }

        throw new EndOfStreamException("The input completed before an identity block was received.");
    }

    private async ValueTask<CtgBlock> ReadCtgAsync(CancellationToken cancellationToken)
    {
        while (await this.TryReadNextAsync(cancellationToken).ConfigureAwait(false) is { } message)
        {
            if (message is CtgMessage ctgMessage)
            {
                return ctgMessage.Block;
            }
        }

        throw new EndOfStreamException("The input completed before a CTG block was received.");
    }

    private async ValueTask<IdBlock> ReadNegotiatedIdentityAsync(
        string requestedRevision,
        CancellationToken cancellationToken)
    {
        while (await this.TryReadNextAsync(cancellationToken).ConfigureAwait(false) is { } message)
        {
            if (message is not IdMessage idMessage)
            {
                continue;
            }

            if (!IsProtocolRevisionSatisfied(idMessage.Block, requestedRevision))
            {
                throw new InvalidOperationException(
                    $"The monitor returned protocol revision '{idMessage.Block.ProtocolRevision}', which does not satisfy requested revision '{requestedRevision}'.");
            }

            return idMessage.Block;
        }

        throw new EndOfStreamException("The input completed before a negotiated identity block was received.");
    }
}
