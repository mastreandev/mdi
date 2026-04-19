using System.Buffers;
using System.IO.Pipelines;

namespace MDI.Philips.M1350;

/// <summary>
/// Provides synchronous and asynchronous session helpers over the currently implemented
/// Philips M1350 reader and command writer primitives.
/// </summary>
public sealed partial class M1350Session : IDisposable, IAsyncDisposable
{
    private readonly PipeReader? input;
    private readonly PipeWriter? ownedOutput;
    private readonly IBufferWriter<byte> output;
    private int asyncReadInUse;

    /// <summary>
    /// Initializes a new instance of the <see cref="M1350Session" /> class.
    /// </summary>
    /// <param name="output">The framed output destination for outbound commands.</param>
    public M1350Session(IBufferWriter<byte> output)
    {
        ArgumentNullException.ThrowIfNull(output);

        this.output = output;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="M1350Session" /> class with a duplex pipe transport.
    /// </summary>
    /// <param name="transport">The duplex transport whose input and output carry framed M1350 traffic.</param>
    public M1350Session(IDuplexPipe transport)
    {
        ArgumentNullException.ThrowIfNull(transport);

        this.input = transport.Input;
        this.output = transport.Output;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="M1350Session" /> class with an asynchronous input reader.
    /// </summary>
    /// <param name="input">The framed input source for inbound blocks.</param>
    /// <param name="output">The framed output destination for outbound commands.</param>
    public M1350Session(PipeReader input, IBufferWriter<byte> output)
        : this(input, output, ownedOutput: null)
    {
    }

    /// <summary>
    /// Creates a session over a duplex stream transport.
    /// </summary>
    /// <param name="transport">The duplex stream carrying framed M1350 traffic.</param>
    /// <param name="leaveOpen"><see langword="true" /> to leave the underlying stream open when the session is disposed.</param>
    public static M1350Session Create(Stream transport, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(transport);

        if (!transport.CanRead)
        {
            throw new ArgumentException("The stream must support reading.", nameof(transport));
        }

        if (!transport.CanWrite)
        {
            throw new ArgumentException("The stream must support writing.", nameof(transport));
        }

        PipeReader input = PipeReader.Create(transport, new StreamPipeReaderOptions(leaveOpen: true));
        PipeWriter output = PipeWriter.Create(transport, new StreamPipeWriterOptions(leaveOpen: leaveOpen));
        return new M1350Session(input, output, output);
    }

    /// <summary>
    /// Creates a session over separate input and output streams.
    /// </summary>
    /// <param name="input">The input stream carrying framed inbound M1350 traffic.</param>
    /// <param name="output">The output stream carrying framed outbound M1350 traffic.</param>
    /// <param name="leaveOpen"><see langword="true" /> to leave the underlying stream or streams open when the session is disposed.</param>
    public static M1350Session Create(Stream input, Stream output, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        if (!input.CanRead)
        {
            throw new ArgumentException("The input stream must support reading.", nameof(input));
        }

        if (!output.CanWrite)
        {
            throw new ArgumentException("The output stream must support writing.", nameof(output));
        }

        bool sharedTransport = ReferenceEquals(input, output);
        PipeReader pipeReader = PipeReader.Create(input, new StreamPipeReaderOptions(leaveOpen: leaveOpen || sharedTransport));
        PipeWriter pipeWriter = PipeWriter.Create(output, new StreamPipeWriterOptions(leaveOpen: leaveOpen));
        return new M1350Session(pipeReader, pipeWriter, pipeWriter);
    }

    private M1350Session(PipeReader input, IBufferWriter<byte> output, PipeWriter? ownedOutput)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);

        this.input = input;
        this.output = output;
        this.ownedOutput = ownedOutput;
    }

    /// <summary>
    /// Completes the input reader when present.
    /// </summary>
    public void Dispose()
    {
        this.ownedOutput?.Complete();
        this.input?.Complete();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Completes the asynchronous input reader when present.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (this.ownedOutput is not null)
        {
            await this.ownedOutput.CompleteAsync().ConfigureAwait(false);
        }

        if (this.input is not null)
        {
            await this.input.CompleteAsync().ConfigureAwait(false);
        }

        GC.SuppressFinalize(this);
    }
}
