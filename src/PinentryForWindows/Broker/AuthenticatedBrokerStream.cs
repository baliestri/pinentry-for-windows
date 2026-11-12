// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Security;

namespace PinentryForWindows.Broker;

internal sealed class AuthenticatedBrokerStream(SslStream stream, string origin) : IAsyncDisposable {
  public SslStream Stream { get; } = stream;
  public string Origin { get; } = origin;

  public ValueTask DisposeAsync()
    => Stream.DisposeAsync();
}
