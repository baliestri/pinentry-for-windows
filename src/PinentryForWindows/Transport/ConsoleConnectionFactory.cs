// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport;
using AssuanLibrary.Transport.Endpoints;
using PinentryForWindows.Transport.Endpoints;

namespace PinentryForWindows.Transport;

internal sealed class ConsoleConnectionFactory : IAssuanConnectionFactory {
  public static readonly ConsoleConnectionFactory Standard = new();

  /// <inheritdoc />
  public IAssuanConnection CreateConnection(IAssuanEndpoint endpoint)
    => endpoint is not ConsoleEndpoint
      ? throw new ArgumentException($"Unsupported endpoint type: {endpoint.GetType().FullName}", nameof(endpoint))
      : new ConsoleConnection();
}
