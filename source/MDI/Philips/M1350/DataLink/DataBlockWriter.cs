using System.Buffers;

using MDI.IO.Hashing;

namespace MDI.Philips.M1350.DataLink;

/// <summary>
/// Writes Philips M1350 data blocks to some destination.
/// </summary>
public sealed class DataBlockWriter : IDisposable
{
    private const int DefaultGrowSize = 256;

    private readonly Crc16 crc16 = new();
    private readonly IBufferWriter<byte> output;
    private readonly DataBlockWriterOptions options;

    private bool isStarted;
    private bool isEnded;

    /// <summary>
    /// Initializes a new instance of <see cref="DataBlockWriter" /> with a
    /// specified <paramref name="output" /> and the default options.
    /// </summary>
    /// <param name="output">The destination for encoded Philips M1350 data.</param>
    public DataBlockWriter(IBufferWriter<byte> output)
        : this(output, new())
    {

    }

    /// <summary>
    /// Initializes a new instance of <see cref="DataBlockWriter" /> with a
    /// specified <paramref name="output" /> and <paramref name="options" />.
    /// </summary>
    /// <param name="output">The destination for the encoded Philips M1350 data.</param>
    /// <param name="options">The options for this instance.</param>
    public DataBlockWriter(IBufferWriter<byte> output, DataBlockWriterOptions options)
    {
        this.output = output;
        this.options = options;
    }

    /// <summary>
    /// Gets the number of bytes written but not yet flushed.
    /// </summary>
    public int BytesPending { get; private set; }

    /// <summary>
    /// Gets the total number of bytes flushed.
    /// </summary>
    public long BytesCommitted { get; private set; }

    /// <summary>
    /// Writes a complete message including start block, data block, end block,
    /// and CRC-16.
    /// </summary>
    /// <param name="value">The value to write.</param>
    public void WriteMessage(ReadOnlySpan<byte> value = default)
    {
        this.WriteStart();
        this.WriteData(value);
        this.WriteEnd();
        this.WriteCrc();
    }

    /// <summary>
    /// Writes a data block value and updates the CRC-16 value.
    /// </summary>
    /// <remarks>
    /// If <see cref="DataBlockWriterOptions.SkipValidation" /> is enabled, the
    /// value will be written without DLE indexing and escaping. This could
    /// result in faster operations and less memory allocation, but it could
    /// also result in incorrect blocks written and undefined behavior for
    /// readers.
    /// </remarks>
    /// <param name="value">The value to write.</param>
    public void WriteData(ReadOnlySpan<byte> value = default)
    {
        if (!this.options.SkipValidation && !this.isStarted)
        {
            throw new InvalidOperationException("Cannot write data before start.");
        }

        int escapeIndex = DataBlockEncoder.GetIndexOfFirstByteToEncode(value);

        if (escapeIndex == -1 || this.options.SkipValidation)
        {
            this.WriteOutput(value);
            this.crc16.Append(value);

            return;
        }

        byte[]? destinationArray = default;

        int length = DataBlockEncoder.GetMaxEscapedLength(value.Length, escapeIndex);
        Span<byte> destination = length <= DataBlockConstants.StackallocByteThreshold
            ? stackalloc byte[DataBlockConstants.StackallocByteThreshold]
            : length <= DataBlockConstants.ArrayPoolByteThreshold
                ? (destinationArray = ArrayPool<byte>.Shared.Rent(length))
                : new byte[length];

        DataBlockEncoder.Encode(value, destination, out int written);

        this.WriteOutput(destination[..written]);
        this.crc16.Append(destination[..written]);

        if (destinationArray is not null)
        {
            ArrayPool<byte>.Shared.Return(destinationArray);
        }
    }

    /// <summary>
    /// Writes then resets the accumulated CRC-16 value.
    /// </summary>
    /// <param name="interrupt">True to write a zeroed CRC value, as in the case of an interrupt.</param>
    public void WriteCrc(bool interrupt = false)
    {
        if (!this.options.SkipValidation && !this.isEnded)
        {
            throw new InvalidOperationException("Cannot write CRC before end.");
        }

        if (interrupt)
        {
            this.crc16.Reset();
            this.WriteOutput(DataBlockConstants.InterruptCrc);
        }
        else
        {
            byte[] crcBytes = this.crc16.GetHashAndReset();
            this.WriteOutput(crcBytes);
        }

        this.isEnded = false;
        this.isStarted = false;
    }

    /// <summary>
    /// Writes a <see cref="DataBlockConstants.StartBlock" />.
    /// </summary>
    public void WriteStart()
    {
        if (!this.options.SkipValidation && this.isStarted && this.isEnded)
        {
            throw new InvalidOperationException("Cannot interrupt in the CRC bytes.");
        }

        // A new start marker begins a new CRC window for this frame.
        this.crc16.Reset();
        this.WriteOutput(DataBlockConstants.StartBlock);
        this.crc16.Append(DataBlockConstants.StartBlock);
        this.isStarted = true;
        this.isEnded = false;
    }

    /// <summary>
    /// Writes an <see cref="DataBlockConstants.EndBlock" />.
    /// </summary>
    public void WriteEnd()
    {
        if (!this.options.SkipValidation && !this.isStarted)
        {
            throw new InvalidOperationException("Cannot write end before start.");
        }

        this.WriteOutput(DataBlockConstants.EndBlock);
        this.crc16.Append(DataBlockConstants.EndBlock);
        this.isEnded = true;
    }

    /// <summary>
    /// Resets the internal state of the writer, but does not affect the
    /// external output destination.
    /// </summary>
    public void Reset()
    {
        this.BytesPending = default;
        this.BytesCommitted = default;

        this.isStarted = default;
        this.isEnded = default;

        this.crc16.Reset();
    }

    /// <summary>
    /// Flushes pending bytes to the external output destination.
    /// </summary>
    public void Flush()
    {
        if (this.BytesPending == 0)
        {
            return;
        }

        this.output.Advance(this.BytesPending);
        this.BytesCommitted += this.BytesPending;
        this.BytesPending = 0;
    }

    /// <summary>
    /// Disposes the instance by flushing any pending bytes to the external
    /// output destination.
    /// </summary>
    public void Dispose()
    {
        this.Flush();
    }

    /// <summary>
    /// Grows the internal memory of the external output destination by
    /// requesting a <see cref="Memory{T}" /> sized to the maximum of the
    /// <see cref="DefaultGrowSize" /> or the size of the value to write.
    /// </summary>
    /// <param name="requiredSize">The size of value to write.</param>
    private void Grow(int requiredSize)
    {
        this.output.Advance(this.BytesPending);
        this.BytesCommitted += this.BytesPending;
        this.BytesPending = 0;

        int sizeHint = Math.Max(DefaultGrowSize, requiredSize);

        this.output.GetMemory(sizeHint);
    }

    /// <summary>
    /// Writes the <paramref name="value" /> to the external output destination
    /// growing the internal memory, if necessary, and incrementing
    /// <see cref="BytesPending" />.
    /// </summary>
    /// <param name="value">The value to write.</param>
    private void WriteOutput(ReadOnlySpan<byte> value)
    {
        this.Grow(value.Length);

        Memory<byte> memory = this.output.GetMemory(value.Length);
        value.CopyTo(memory.Span[this.BytesPending..]);
        this.BytesPending += value.Length;
    }
}
