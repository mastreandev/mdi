using System.Buffers;
using System.Buffers.Binary;
using System.Text;

using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.EventMessage;
using MDI.Philips.M1350.Application.Failure;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Nibp;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.Application.SpO2;
using MDI.Philips.M1350.Application.Temperature;
using MDI.Philips.M1350.DataLink;
using MDI.Philips.M1350.Simulator.Application.Identity;

namespace MDI.Philips.M1350.Simulator;

/// <summary>
/// Writes framed FM-originated Philips M1350 blocks.
/// </summary>
public static class M1350FmWriter
{
    /// <summary>
    /// Writes a framed monitor-originated block for a replayed Philips M1350 message.
    /// </summary>
    public static void WriteMessage(IBufferWriter<byte> output, M1350Message message)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(message);

        if (message.Direction == M1350MessageDirection.Outbound)
        {
            throw new ArgumentException("Outbound replay messages cannot be emitted as monitor-originated traffic.", nameof(message));
        }

        switch (message)
        {
            case IdMessage id:
                WriteIdentity(output, id.Block);
                break;

            case CtgMessage ctg:
                WriteCtg(output, ctg.Block);
                break;

            case NoteMessage note:
                WriteNote(output, note.Block);
                break;

            case FailureMessage failure:
                WriteFailure(output, failure.Block);
                break;

            case EventMarkerMessage:
                WriteEventMarker(output);
                break;

            case NibpMessage nibp:
                WriteNibp(output, nibp.Block);
                break;

            case SpO2Message spo2:
                WriteSpO2(output, spo2.Block);
                break;

            case TemperatureMessage temperature:
                WriteTemperature(output, temperature.Block);
                break;

            default:
                throw new NotSupportedException($"Replay emission does not support message type '{message.GetType().Name}'.");
        }
    }

    /// <summary>
    /// Writes a framed monitor identity block.
    /// </summary>
    public static void WriteIdentity(IBufferWriter<byte> output, in IdBlock block)
    {
        ArgumentNullException.ThrowIfNull(output);

        Span<byte> payload = stackalloc byte[IdBlockEncoder.EncodedLength];

        bool encoded = IdBlockEncoder.TryEncode(block, payload, out int bytesWritten);
        if (!encoded)
        {
            throw new ArgumentException(
                "Identity fields must be fixed-width ASCII values that match the Philips M1350 I-block layout.",
                nameof(block));
        }

        WriteMessage(output, payload[..bytesWritten]);
    }

    /// <summary>
    /// Writes a framed CTG data block.
    /// </summary>
    public static void WriteCtg(IBufferWriter<byte> output, in CtgBlock block)
    {
        ArgumentNullException.ThrowIfNull(output);

        Span<byte> payload = stackalloc byte[CtgBlockEncoder.EncodedLength];

        bool encoded = CtgBlockEncoder.TryEncode(block, payload, out int bytesWritten);
        if (!encoded)
        {
            throw new InvalidOperationException("Failed to encode Philips M1350 CTG payload.");
        }

        WriteMessage(output, payload[..bytesWritten]);
    }

    private static void WriteNote(IBufferWriter<byte> output, in NoteBlock block)
    {
        Span<byte> payload = stackalloc byte[NoteBlockEncoder.MaximumPayloadLength];

        if (!NoteBlockEncoder.TryEncode(block, payload, out int bytesWritten))
        {
            throw new ArgumentException(
                "Replay note text must be ASCII, non-empty, and fit within 28 printable characters including the optional user ID.",
                nameof(block));
        }

        WriteMessage(output, payload[..bytesWritten]);
    }

    private static void WriteFailure(IBufferWriter<byte> output, in FailureBlock block)
    {
        Span<byte> payload = stackalloc byte[FailureBlockParser.PayloadLength];
        payload[0] = FailureBlockParser.TypeByte;

        if (!TryWriteAsciiExact(block.ErrorCode, payload[1..4]))
        {
            throw new ArgumentException("Replay failure codes must be 3-character ASCII values.", nameof(block));
        }

        WriteMessage(output, payload);
    }

    private static void WriteEventMarker(IBufferWriter<byte> output)
    {
        Span<byte> payload = [EventMessageBlockParser.TypeByte, EventMessageBlockParser.SubtypeByte];
        WriteMessage(output, payload);
    }

    private static void WriteNibp(IBufferWriter<byte> output, in NibpBlock block)
    {
        Span<byte> payload = stackalloc byte[NibpBlockParser.PayloadLength];
        payload[0] = NibpBlockParser.TypeByte;
        BinaryPrimitives.WriteUInt16BigEndian(payload[1..3], block.SystolicPressure);
        BinaryPrimitives.WriteUInt16BigEndian(payload[3..5], block.DiastolicPressure);
        BinaryPrimitives.WriteUInt16BigEndian(payload[5..7], block.MeanPressure);
        BinaryPrimitives.WriteUInt16BigEndian(payload[7..9], block.MaternalHeartRate);
        WriteMessage(output, payload);
    }

    private static void WriteSpO2(IBufferWriter<byte> output, in SpO2Block block)
    {
        Span<byte> payload = stackalloc byte[SpO2BlockParser.PayloadLength];
        payload[0] = SpO2BlockParser.TypeByte;
        payload[1] = block.OxygenSaturation;
        BinaryPrimitives.WriteUInt16BigEndian(payload[2..4], block.MaternalHeartRate);
        WriteMessage(output, payload);
    }

    private static void WriteTemperature(IBufferWriter<byte> output, in TemperatureBlock block)
    {
        Span<byte> payload = [TemperatureBlockParser.TypeByte, block.RawValue];
        WriteMessage(output, payload);
    }

    private static bool TryWriteAsciiExact(string value, Span<byte> destination)
    {
        if (value.Length != destination.Length)
        {
            return false;
        }

        return Encoding.ASCII.GetBytes(value, destination) == destination.Length;
    }

    private static void WriteMessage(IBufferWriter<byte> output, ReadOnlySpan<byte> payload)
    {
        using DataBlockWriter writer = new(output);
        writer.WriteMessage(payload);
    }
}
