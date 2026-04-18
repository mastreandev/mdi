using System.Buffers;

using MDI.Philips.M1350.Application.CTG;
using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.DataLink;
using MDI.Philips.M1350.Simulator.Application.Identity;

namespace MDI.Philips.M1350.Simulator;

/// <summary>
/// Writes framed FM-originated Philips M1350 blocks.
/// </summary>
public static class M1350FmWriter
{
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

    private static void WriteMessage(IBufferWriter<byte> output, ReadOnlySpan<byte> payload)
    {
        using DataBlockWriter writer = new(output);
        writer.WriteMessage(payload);
    }
}
