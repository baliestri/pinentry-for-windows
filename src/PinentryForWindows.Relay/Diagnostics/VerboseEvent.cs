// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Relay.Diagnostics;

internal enum VerboseEvent {
  CommandStarted,
  IdentityLoaded,
  VsockConnecting,
  VsockConnected,
  TlsAuthenticated,
  RelayFinished
}
