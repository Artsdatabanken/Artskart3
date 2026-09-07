namespace Artskart3.Workers.Export;

/// <summary>
/// Teller antall bytes som skrives gjennom strømmen.
///
/// CSV-en streames rett til blob storage, så vi har ingen MemoryStream å lese
/// Length fra når filstørrelsen skal lagres på jobben. Denne wrapperen holder
/// tellingen underveis i stedet.
///
/// Eier ikke den underliggende strømmen og lukker den ikke — kalleren styrer
/// livsløpet til blob-strømmen selv.
/// </summary>
internal sealed class CountingStream(Stream inner) : Stream
{
    public long BytesWritten { get; private set; }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => BytesWritten;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        BytesWritten += count;
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        inner.Write(buffer);
        BytesWritten += buffer.Length;
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await inner.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
        BytesWritten += count;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await inner.WriteAsync(buffer, cancellationToken);
        BytesWritten += buffer.Length;
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
