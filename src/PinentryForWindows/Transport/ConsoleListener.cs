// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;
using PinentryForWindows.Transport.Endpoints;

namespace PinentryForWindows.Transport;

internal sealed class ConsoleListener : IAssuanListener {
  private bool _accepted;

  /// <inheritdoc />
  public IAssuanEndpoint Endpoint => ConsoleEndpoint.Standard;

  /// <inheritdoc />
  public IAssuanConnection Accept() {
    if (_accepted) {
      throw new InvalidOperationException("ConsoleListener can only accept one connection.");
    }

    _accepted = true;
    return new ConsoleConnection();
  }

  /// <inheritdoc />
  public ValueTask<IAssuanConnection> AcceptAsync(CancellationToken ct = new()) {
    if (_accepted) {
      throw new InvalidOperationException("ConsoleListener can only accept one connection.");
    }

    _accepted = true;
    return new ValueTask<IAssuanConnection>(new ConsoleConnection());
  }
}
