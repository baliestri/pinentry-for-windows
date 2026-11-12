// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace PinentryForWindows.Relay.Transport;

// .NET's Socket wrapper does not reliably update Connected for an imported AF_VSOCK handle.
// Native I/O avoids a spurious zero read after the TLS handshake.
[SupportedOSPlatform("linux")]
internal sealed partial class VsockStream(Socket socket) : Stream {
  private const int EINTR = 4;
  private const int EAGAIN = 11;
  private const int MSG_NOSIGNAL = 0x4000;
  private const short POLLIN = 1;
  private const short POLLOUT = 4;
  private const short POLLERR = 8;
  private const short POLLHUP = 16;

  public override bool CanRead => true;
  public override bool CanWrite => true;
  public override bool CanSeek => false;
  public override long Length => throw new NotSupportedException();
  public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

  public override int Read(byte[] buffer, int offset, int count)
    => ReadNative(socket.SafeHandle, buffer, offset, count, CancellationToken.None);

  public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) {
    if (buffer.IsEmpty) {
      return 0;
    }

    var scratch = new byte[Math.Min(buffer.Length, 4096)];
    try {
      var count = await Task.Run(() => ReadNative(socket.SafeHandle, scratch, 0, scratch.Length, ct), ct)
        .ConfigureAwait(false);
      scratch.AsMemory(0, count).CopyTo(buffer);
      return count;
    }
    finally { CryptographicOperations.ZeroMemory(scratch); }
  }

  public override void Write(byte[] buffer, int offset, int count)
    => WriteNative(socket.SafeHandle, buffer, offset, count, CancellationToken.None);

  public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
    var scratch = buffer.ToArray();
    try {
      await Task.Run(() => WriteNative(socket.SafeHandle, scratch, 0, scratch.Length, ct), ct)
        .ConfigureAwait(false);
    }
    finally { CryptographicOperations.ZeroMemory(scratch); }
  }

  public override void Flush() { }

  public override Task FlushAsync(CancellationToken ct)
    => Task.CompletedTask;

  public override long Seek(long offset, SeekOrigin origin)
    => throw new NotSupportedException();

  public override void SetLength(long value)
    => throw new NotSupportedException();

  private static unsafe int ReadNative(SafeSocketHandle handle, byte[] destination,
    int offset, int count, CancellationToken ct) {
    if (count == 0) {
      return 0;
    }

    while (true) {
      WaitFor(handle, POLLIN, ct);
      fixed (byte* pointer = &destination[offset]) {
        var read = NativeRecv(handle, pointer, (nuint)count, 0);
        if (read >= 0) {
          return checked((int)read);
        }
      }

      var error = Marshal.GetLastPInvokeError();
      if (error is EINTR or EAGAIN) {
        continue;
      }

      throw new SocketException(error);
    }
  }

  private static unsafe void WriteNative(SafeSocketHandle handle, byte[] source,
    int offset, int count, CancellationToken ct) {
    while (count > 0) {
      WaitFor(handle, POLLOUT, ct);
      nint written;
      fixed (byte* pointer = &source[offset]) {
        written = NativeSend(handle, pointer, (nuint)count, MSG_NOSIGNAL);
      }

      switch (written) {
        case > 0:
          offset += checked((int)written);
          count -= checked((int)written);
          continue;
        case 0:
          throw new EndOfStreamException("VSock closed during write.");
      }

      var error = Marshal.GetLastPInvokeError();
      if (error is EINTR or EAGAIN) {
        continue;
      }

      throw new SocketException(error);
    }
  }

  private static void WaitFor(SafeSocketHandle handle, short requested, CancellationToken ct) {
    var descriptor = new PollDescriptor {
      Fd = checked((int)handle.DangerousGetHandle()),
      Events = (short)(requested | POLLERR | POLLHUP)
    };
    while (true) {
      ct.ThrowIfCancellationRequested();
      var ready = NativePoll(ref descriptor, 1, 100);
      switch (ready) {
        case > 0:
          return;
        case 0:
          continue;
      }

      var error = Marshal.GetLastPInvokeError();
      if (error != EINTR) {
        throw new SocketException(error);
      }
    }
  }

  [LibraryImport("libc", EntryPoint = "poll", SetLastError = true)]
  private static partial int NativePoll(ref PollDescriptor descriptor, nuint count, int timeoutMilliseconds);

  [LibraryImport("libc", EntryPoint = "recv", SetLastError = true)]
  private static unsafe partial nint NativeRecv(SafeSocketHandle handle, byte* data, nuint length, int flags);

  [LibraryImport("libc", EntryPoint = "send", SetLastError = true)]
  private static unsafe partial nint NativeSend(SafeSocketHandle handle, byte* data, nuint length, int flags);

  [StructLayout(LayoutKind.Sequential)]
  private struct PollDescriptor {
    public int Fd;
    public short Events;
    public short Returned;
  }
}
