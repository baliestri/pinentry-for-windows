// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;

namespace PinentryForWindows.Relay.Transport;

[SupportedOSPlatform("linux")]
internal sealed class VsockEndpoint(int cid, int port) : EndPoint {
  public override AddressFamily AddressFamily => (AddressFamily)40;

  public override SocketAddress Serialize() {
    var address = new SocketAddress(AddressFamily.Unspecified, 16);
    BitConverter.TryWriteBytes(address.Buffer.Span[..2], (ushort)AddressFamily);
    BitConverter.TryWriteBytes(address.Buffer.Span[4..8], port);
    BitConverter.TryWriteBytes(address.Buffer.Span[8..12], cid);
    return address;
  }

  public override EndPoint Create(SocketAddress address) {
    if (address.Size < 16 ||
        BitConverter.ToUInt16(address.Buffer.Span[..2]) != (ushort)AddressFamily) {
      throw new ArgumentException("Invalid VSock address.", nameof(address));
    }

    return new VsockEndpoint(BitConverter.ToInt32(address.Buffer.Span[8..12]),
      BitConverter.ToInt32(address.Buffer.Span[4..8]));
  }
}
