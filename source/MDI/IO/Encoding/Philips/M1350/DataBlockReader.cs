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
                    state = State.Data;
                    break;

                case (State.StartEscape, _):
                    state = State.None;
                    break;

                case (State.Data, DataBlockConstants.DLE):
                    if (reader.IsNext(DataBlockConstants.DLE, advancePast: true))
                    {
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

                case (State.DataEscape, DataBlockConstants.STX):
                    // A new start marker interrupts the current incomplete block.
                    startPosition = escapePosition;
                    dataPosition = reader.Position;
                    state = State.Data;
                    break;

                case (State.DataEscape, _):
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
