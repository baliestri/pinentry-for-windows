// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Transport;

internal sealed class StandardIoDuplexStream(Stream input, Stream output) : Stream {
  public override bool CanRead => input.CanRead;
  public override bool CanWrite => output.CanWrite;
  public override bool CanSeek => false;
  public override long Length => throw new NotSupportedException();
  public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

  public override int Read(byte[] buffer, int offset, int count)
    => input.Read(buffer, offset, count);

  public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    => input.ReadAsync(buffer, ct);

  public override void Write(byte[] buffer, int offset, int count) => output.Write(buffer, offset, count);

  public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    => output.WriteAsync(buffer, ct);

  public override void Flush()
    => output.Flush();

  public override Task FlushAsync(CancellationToken ct)
    => output.FlushAsync(ct);

  public override long Seek(long offset, SeekOrigin origin)
    => throw new NotSupportedException();

  public override void SetLength(long value)
    => throw new NotSupportedException();

  protected override void Dispose(bool disposing) {
    if (disposing) {
      input.Dispose();
      output.Dispose();
    }

    base.Dispose(disposing);
  }

  public override async ValueTask DisposeAsync() {
    await input.DisposeAsync().ConfigureAwait(false);
    await output.DisposeAsync().ConfigureAwait(false);
    await base.DisposeAsync().ConfigureAwait(false);
  }
}
