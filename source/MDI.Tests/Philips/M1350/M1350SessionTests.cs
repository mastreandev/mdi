using System.Buffers;
using System.IO.Pipelines;

using MDI.Philips.M1350.Application.Identity;
using MDI.Philips.M1350.DataLink;

namespace MDI.Tests.Philips.M1350;

[TestClass]
public sealed partial class M1350SessionTests
{
    public TestContext TestContext { get; set; } = null!;

    private static void AssertPayload(ref ReadOnlySequence<byte> buffer, byte[] expectedPayload)
    {
        bool result = DataBlockReader.TryRead(ref buffer, out ReadOnlySequence<byte> payload);

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(expectedPayload, payload.ToArray());
    }

    private static async Task WriteInputAsync(PipeWriter writer, CancellationToken cancellationToken = default, params byte[][] payloads)
    {
        ArrayBufferWriter<byte> output = new();

        foreach (byte[] payload in payloads)
        {
            using DataBlockWriter dataBlockWriter = new(output);
            dataBlockWriter.WriteMessage(payload);
        }

        await writer.WriteAsync(output.WrittenMemory, cancellationToken);
        await writer.CompleteAsync();
    }

    private static MemoryStream BuildInputStream(params byte[][] payloads)
    {
        ArrayBufferWriter<byte> output = new();

        foreach (byte[] payload in payloads)
        {
            using DataBlockWriter dataBlockWriter = new(output);
            dataBlockWriter.WriteMessage(payload);
        }

        return new MemoryStream(output.WrittenMemory.ToArray(), writable: false);
    }

    private static IdBlock BuildIdentityBlock(string protocolRevision)
    {
        return new IdBlock("M1350A", protocolRevision, "A.03.00", "3019G10010");
    }

    private static byte[] BuildIdentityPayload(string protocolRevision = "A20")
    {
        byte[] payload = new byte[27];
        payload[0] = (byte)'I';
        "M1350A"u8.CopyTo(payload.AsSpan(1, 6));
        payload[7] = (byte)protocolRevision[0];
        payload[8] = (byte)protocolRevision[1];
        payload[9] = (byte)protocolRevision[2];
        "A.03.00"u8.CopyTo(payload.AsSpan(10, 7));
        "3019G10010"u8.CopyTo(payload.AsSpan(17, 10));
        return payload;
    }

    private sealed class TestDuplexPipe(PipeReader input, PipeWriter output) : IDuplexPipe
    {
        public PipeReader Input { get; } = input;

        public PipeWriter Output { get; } = output;
    }

    private sealed class TestDuplexStream(Stream input, Stream output) : Stream
    {
        public override bool CanRead => input.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => output.CanWrite;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            output.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return input.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            return input.Read(buffer);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return input.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            output.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            output.Write(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return output.WriteAsync(buffer, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                input.Dispose();
                output.Dispose();
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await input.DisposeAsync();
            await output.DisposeAsync();
            await base.DisposeAsync();
        }
    }
}
