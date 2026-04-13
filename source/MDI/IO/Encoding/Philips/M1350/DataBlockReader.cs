using System.Buffers;
using System.Buffers.Binary;

using MDI.IO.Hashing;

namespace MDI.IO.Encoding.Philips.M1350;

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

        Span<byte> expectedCrc = stackalloc byte[sizeof(ushort)];
        Span<byte> actualCrc = stackalloc byte[sizeof(ushort)];

        while (!reader.End)
        {
            lastPosition = reader.Position;

            _ = reader.TryRead(out next);

            switch (state, next)
            {
                case (State.None, DataBlockConstants.DLE):
                    escapePosition = lastPosition;
                    state = State.Escape;
                    break;

                case (State.None, _):
                    break;

                case (State.Escape, DataBlockConstants.STX):
                    startPosition = escapePosition;
                    dataPosition = reader.Position;
                    state = State.Data;
                    break;

                case (State.Escape, DataBlockConstants.ETX):
                    endPosition = escapePosition;

                    if (!reader.TryRead(out expectedCrc[0]) || !reader.TryRead(out expectedCrc[1]))
                    {
                        break;
                    }

                    block = buffer.Slice(dataPosition, endPosition);

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

                    state = State.None;
                    break;

                case (State.Data, DataBlockConstants.DLE):
                    if (reader.IsNext(DataBlockConstants.DLE, advancePast: true))
                    {
                        break;
                    }

                    escapePosition = lastPosition;
                    state = State.Escape;
                    break;

                case (State.Data, _):
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        block = default;
        buffer = state == State.None
            ? buffer.Slice(reader.Position)
            : buffer.Slice(startPosition);

        return false;
    }

    private enum State
    {
        None = 0,

        /// <summary>
        /// Read the end of block.
        /// </summary>
        CRC,

        /// <summary>
        /// Read the start of block.
        /// </summary>
        Data,

        /// <summary>
        /// Read a DLE byte.
        /// </summary>
        Escape,
    }
}
