using System.Buffers;

using MDI.Philips.M1350.Application;
using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.Application.Notes;
using MDI.Philips.M1350.DataLink;

namespace MDI.Philips.M1350;

/// <summary>
/// Writes framed Philips M1350 session commands.
/// </summary>
public static class M1350CommandWriter
{
    /// <summary>
    /// Writes a framed request for the monitor identity block (<c>?I</c>).
    /// </summary>
    public static void WriteRequestIdentity(IBufferWriter<byte> output)
    {
        WriteRequest(output, IdBlockParser.TypeByte);
    }

    /// <summary>
    /// Writes a framed request for the CTG block (<c>?C</c>).
    /// </summary>
    public static void WriteRequestCtg(IBufferWriter<byte> output)
    {
        WriteRequest(output, CtgBlockParser.TypeByte);
    }

    /// <summary>
    /// Writes a framed command that starts automatic CTG transmission (<c>G</c>).
    /// </summary>
    public static void WriteStartAutoSend(IBufferWriter<byte> output)
    {
        Span<byte> payload = stackalloc byte[GoAutoSendCommandEncoder.EncodedLength];

        bool encoded = GoAutoSendCommandEncoder.TryEncode(payload, out int bytesWritten);
        if (!encoded)
        {
            throw new InvalidOperationException("Failed to encode go-auto-send command.");
        }

        WriteMessage(output, payload[..bytesWritten]);
    }

    /// <summary>
    /// Writes a framed command that halts automatic CTG transmission (<c>H</c>).
    /// </summary>
    public static void WriteHaltAutoSend(IBufferWriter<byte> output)
    {
        Span<byte> payload = stackalloc byte[HaltAutoSendCommandEncoder.EncodedLength];

        bool encoded = HaltAutoSendCommandEncoder.TryEncode(payload, out int bytesWritten);
        if (!encoded)
        {
            throw new InvalidOperationException("Failed to encode halt-auto-send command.");
        }

        WriteMessage(output, payload[..bytesWritten]);
    }

    /// <summary>
    /// Writes a framed host-originated note block with an optional user identifier.
    /// </summary>
    public static void WriteNote(IBufferWriter<byte> output, string text, string userId = "")
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(text);

        NoteBlock block = new(userId, text);
        Span<byte> payload = stackalloc byte[NoteBlockEncoder.MaximumPayloadLength];

        bool encoded = NoteBlockEncoder.TryEncode(block, payload, out int bytesWritten);
        if (!encoded)
        {
            throw new ArgumentException(
                "Note text must be ASCII, non-empty, and fit within 28 printable characters including the optional user ID.",
                nameof(text));
        }

        WriteMessage(output, payload[..bytesWritten]);
    }

    /// <summary>
    /// Writes a framed generic request block, for example <c>?I</c> or <c>?C</c>.
    /// </summary>
    public static void WriteRequest(IBufferWriter<byte> output, byte requestedType)
    {
        Span<byte> payload = stackalloc byte[RequestBlockEncoder.EncodedLength];

        bool encoded = RequestBlockEncoder.TryEncode(requestedType, payload, out int bytesWritten);
        if (!encoded)
        {
            throw new InvalidOperationException("Failed to encode request block.");
        }

        WriteMessage(output, payload[..bytesWritten]);
    }

    /// <summary>
    /// Writes a framed protocol revision change request, for example <c>VA20</c>.
    /// </summary>
    public static void WriteProtocolRevisionChange(IBufferWriter<byte> output, string requestedRevision)
    {
        ArgumentNullException.ThrowIfNull(requestedRevision);

        Span<byte> payload = stackalloc byte[ProtocolRevisionChangeRequestEncoder.EncodedLength];

        bool encoded = ProtocolRevisionChangeRequestEncoder.TryEncode(
            requestedRevision.AsSpan(),
            payload,
            out int bytesWritten);

        if (!encoded)
        {
            throw new ArgumentException(
                "Protocol revision must be a 3-character ASCII token, for example A20.",
                nameof(requestedRevision));
        }

        WriteMessage(output, payload[..bytesWritten]);
    }

    private static void WriteMessage(IBufferWriter<byte> output, ReadOnlySpan<byte> payload)
    {
        using DataBlockWriter writer = new(output);
        writer.WriteMessage(payload);
    }
}
