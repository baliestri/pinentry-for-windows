// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PinentryForWindows.Relay.Transport;

[SupportedOSPlatform("linux")]
internal static unsafe partial class VsockConnector {
  private const int AF_VSOCK = 40;
  private const int SOCK_STREAM = 1;
  private const int SOCK_CLOEXEC = 0x80000;

  public static Socket Connect(int port, TimeSpan timeout, CancellationToken ct) {
    if (port < 1 ||
        timeout <= TimeSpan.Zero) {
      throw new ArgumentOutOfRangeException(nameof(port));
    }

    var handle = NativeSocket(AF_VSOCK, SOCK_STREAM | SOCK_CLOEXEC, 0);
    if (handle.IsInvalid) {
      throw new SocketException(Marshal.GetLastPInvokeError());
    }

    Socket socket;
    try { socket = new Socket(handle); }
    catch {
      handle.Dispose();
      throw;
    }

    try {
      // Linux AF_VSOCK option 6 accepts a timeval and bounds a blocking connect.
      Span<byte> timeval = stackalloc byte[16];
      BinaryPrimitives.WriteInt64LittleEndian(timeval[..8], (long)timeout.TotalSeconds);
      BinaryPrimitives.WriteInt64LittleEndian(timeval[8..], timeout.Microseconds + timeout.Milliseconds * 1000L);
      socket.SetRawSocketOption(AF_VSOCK, 6, timeval);
      ct.ThrowIfCancellationRequested();
      using var cancellation = ct.Register(static state => ((Socket)state!).Dispose(), socket);
      try { socket.Connect(new VsockEndpoint(2, port)); }
      catch (Exception error) when (ct.IsCancellationRequested &&
                                    error is SocketException or ObjectDisposedException) {
        throw new OperationCanceledException(ct);
      }

      ct.ThrowIfCancellationRequested();
      return socket;
    }
    catch {
      socket.Dispose();
      throw;
    }
  }

  [LibraryImport("libc", EntryPoint = "socket", SetLastError = true)]
  private static partial SafeSocketHandle NativeSocket(int family, int type, int protocol);
}
