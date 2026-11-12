// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Relay.Diagnostics;

internal static class VerboseLog {
  private readonly static bool _enabled =
    string.Equals(Environment.GetEnvironmentVariable("PFW_VERBOSE"), "1", StringComparison.Ordinal);

  public static void Write(VerboseEvent eventType) {
    if (!_enabled) {
      return;
    }

    var message = eventType switch {
      VerboseEvent.CommandStarted => "command started",
      VerboseEvent.IdentityLoaded => "client identity loaded",
      VerboseEvent.VsockConnecting => "connecting to Windows broker",
      VerboseEvent.VsockConnected => "vsock connected",
      VerboseEvent.TlsAuthenticated => "TLS authenticated",
      VerboseEvent.RelayFinished => "relay session finished",
      var _ => "operation"
    };
    try { Console.Error.WriteLine("[pfw:relay] " + message); }
    catch (Exception error) when (error is IOException or ObjectDisposedException) {
      // Optional diagnostics must not interrupt the relay.
    }
  }
}
