// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Sockets;

namespace PinentryForWindows.Broker;

internal sealed class HyperVEndpoint(Guid vmId) : EndPoint {
  public const int VSOCK_PORT = 44986;
  public readonly static Guid ServiceId = new("0000afba-facb-11e6-bd58-64006a7986d3");
  public override AddressFamily AddressFamily => (AddressFamily)34;

  public override SocketAddress Serialize() {
    var address = new SocketAddress(AddressFamily, 36);
    vmId.TryWriteBytes(address.Buffer.Span[4..20]);
    ServiceId.TryWriteBytes(address.Buffer.Span[20..36]);
    return address;
  }

  public override EndPoint Create(SocketAddress address) {
    if (address.Size < 36 ||
        address.Family != AddressFamily) {
      throw new ArgumentException("Invalid Hyper-V socket address.", nameof(address));
    }

    return new HyperVEndpoint(new Guid(address.Buffer.Span[4..20]));
  }
}
