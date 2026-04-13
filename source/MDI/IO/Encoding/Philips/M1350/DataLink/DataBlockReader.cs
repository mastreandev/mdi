using System.Buffers;
using System.Buffers.Binary;

using MDI.IO.Hashing;

namespace MDI.IO.Encoding.Philips.M1350.DataLink;

public static class DataBlockReader
{
    // TODO: diagnostic API?
    // TODO: return ReadResult?
    public static bool TryRead(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> block)
    {
        State state = State.None;
        byte next = default;
        Crc16 crc = new();
        SequenceReader<byte> reader = new(buffer);

        SequencePosition lastPosition = default;
        SequencePosition escapePosition = default;
        SequencePosition startPosition = default;
        SequencePosition dataPosition = default;
        SequencePosition endPosition = default;
        bool escapedDleSeen = false;

        Span<byte> actualCrc = stackalloc byte[sizeof(ushort)];

        while (!reader.End)
        {
            lastPosition = reader.Position;

            _ = reader.TryRead(out next);

            switch (state, next)
            {
                case (State.None, DataBlockConstants.DLE):
                    escapePosition = lastPosition;
                    state = State.StartEscape;
                    break;

                case (State.None, _):
                    break;

                case (State.StartEscape, DataBlockConstants.STX):
                    startPosition = escapePosition;
                    dataPosition = reader.Position;
                    escapedDleSeen = false;
                    state = State.Data;
                    break;

                case (State.StartEscape, _):
                    escapedDleSeen = false;
                    state = State.None;
                    break;

                case (State.Data, DataBlockConstants.DLE):
                    if (reader.IsNext(DataBlockConstants.DLE, advancePast: true))
                    {
                        escapedDleSeen = true;
                        break;
                    }

                    escapePosition = lastPosition;
                    state = State.DataEscape;
                    break;

                case (State.Data, _):
                    break;

                case (State.DataEscape, DataBlockConstants.ETX):
                    endPosition = escapePosition;

                    if (!reader.TryRead(out _) || !reader.TryRead(out _))
                    {
                        break;
                    }

                    ReadOnlySequence<byte> rawBlock = buffer.Slice(dataPosition, endPosition);
                    block = escapedDleSeen
                        ? DecodeEscapedPayload(rawBlock)
                        : rawBlock;

                    ReadOnlySequence<byte> frameWithCrc = buffer.Slice(startPosition, reader.Position);

                    foreach (ReadOnlyMemory<byte> memory in frameWithCrc)
                    {
                        crc.Append(memory.Span);
                    }
                    _ = crc.TryGetHashAndReset(actualCrc, out _);

                    if (BinaryPrimitives.ReadUInt16BigEndian(actualCrc) == 0)
                    {
                        buffer = buffer.Slice(reader.Position);
                        return true;
                    }

                    escapedDleSeen = false;
                    state = State.None;
                    break;

                case (State.DataEscape, DataBlockConstants.STX):
                    // A new start marker interrupts the current incomplete block.
                    startPosition = escapePosition;
                    dataPosition = reader.Position;
                    escapedDleSeen = false;
                    state = State.Data;
                    break;

                case (State.DataEscape, _):
                    escapedDleSeen = false;
                    state = State.None;
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        block = default;
        buffer = state switch
        {
            State.None => buffer.Slice(reader.Position),
            State.StartEscape => buffer.Slice(escapePosition),
            State.Data => buffer.Slice(startPosition),
            State.DataEscape => buffer.Slice(startPosition),
            _ => throw new InvalidOperationException("Unexpected reader state."),
        };

        return false;
    }

    private static ReadOnlySequence<byte> DecodeEscapedPayload(ReadOnlySequence<byte> source)
    {
        SequenceReader<byte> reader = new(source);

        int escapedCount = 0;
        while (reader.TryRead(out byte b))
        {
            if (b == DataBlockConstants.DLE && reader.IsNext(DataBlockConstants.DLE, advancePast: true))
            {
                escapedCount++;
            }
        }

        int decodedLength = checked((int)source.Length - escapedCount);
        byte[] decoded = new byte[decodedLength];

        reader = new(source);

        int index = 0;
        while (reader.TryRead(out byte b))
        {
            if (b == DataBlockConstants.DLE && reader.IsNext(DataBlockConstants.DLE, advancePast: true))
            {
                decoded[index++] = DataBlockConstants.DLE;
                continue;
            }

            decoded[index++] = b;
        }

        return new ReadOnlySequence<byte>(decoded);
    }

    private enum State
    {
        None = 0,

        /// <summary>
        /// Read a DLE while scanning for the start block.
        /// </summary>
        StartEscape,

        /// <summary>
        /// Read block data.
        /// </summary>
        Data,

        /// <summary>
        /// Read a DLE while inside a block.
        /// </summary>
        DataEscape,
    }
}
