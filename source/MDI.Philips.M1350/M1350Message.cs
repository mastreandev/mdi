using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.EventMessage;
using MDI.Philips.M1350.Application.Failure;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Nibp;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.Application.SpO2;
using MDI.Philips.M1350.Application.Temperature;

namespace MDI.Philips.M1350;

/// <summary>
/// Represents a Philips M1350 application payload observed at the session boundary.
/// </summary>
/// <param name="TypeByte">The application-layer type byte for the underlying payload.</param>
public abstract record M1350Message(byte TypeByte)
{
    /// <summary>
    /// Gets the observed message direction.
    /// </summary>
    public M1350MessageDirection Direction { get; init; } = M1350MessageDirection.Inbound;

    /// <summary>
    /// Gets the monotonic offset at which the session observed this message, when known.
    /// </summary>
    public TimeSpan? ReceivedOffset { get; init; }

    internal M1350Message WithObservation(TimeSpan receivedOffset, M1350MessageDirection direction)
    {
        return this with
        {
            Direction = direction,
            ReceivedOffset = receivedOffset,
        };
    }
}

/// <summary>
/// Represents a parsed CTG C-block message.
/// </summary>
/// <param name="Block">The parsed CTG block.</param>
public sealed record CtgMessage(CtgBlock Block) : M1350Message(CtgBlockParser.TypeByte);

/// <summary>
/// Represents a parsed monitor identity I-block message.
/// </summary>
/// <param name="Block">The parsed identity block.</param>
public sealed record IdMessage(IdBlock Block) : M1350Message(IdBlockParser.TypeByte);

/// <summary>
/// Represents a parsed maternal non-invasive blood pressure P-block message.
/// </summary>
/// <param name="Block">The parsed NIBP block.</param>
public sealed record NibpMessage(NibpBlock Block) : M1350Message(NibpBlockParser.TypeByte);

/// <summary>
/// Represents a parsed maternal temperature T-block message.
/// </summary>
/// <param name="Block">The parsed temperature block.</param>
public sealed record TemperatureMessage(TemperatureBlock Block) : M1350Message(TemperatureBlockParser.TypeByte);

/// <summary>
/// Represents a parsed maternal oxygen saturation S-block message.
/// </summary>
/// <param name="Block">The parsed SpO2 block.</param>
public sealed record SpO2Message(SpO2Block Block) : M1350Message(SpO2BlockParser.TypeByte);

/// <summary>
/// Represents a parsed event marker MM-block message.
/// </summary>
/// <param name="Block">The parsed event-marker block.</param>
public sealed record EventMarkerMessage(EventMessageBlock Block) : M1350Message(EventMessageBlockParser.TypeByte);

/// <summary>
/// Represents a parsed nursing-note N-block message.
/// </summary>
/// <param name="Block">The parsed note block.</param>
public sealed record NoteMessage(NoteBlock Block) : M1350Message(NoteBlockParser.TypeByte);

/// <summary>
/// Represents a parsed failure F-block message.
/// </summary>
/// <param name="Block">The parsed failure block.</param>
public sealed record FailureMessage(FailureBlock Block) : M1350Message(FailureBlockParser.TypeByte);
