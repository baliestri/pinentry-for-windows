// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Runtime.Versioning;
using PinentryForWindows.Relay.Transport;

namespace PinentryForWindows.Relay.Tests;

public sealed class VsockConnectorTests {
  [Fact]
  [SupportedOSPlatform("linux")]
  public void Missing_service_cannot_block_beyond_the_connection_deadline() {
    if (!OperatingSystem.IsLinux() ||
        !File.Exists("/dev/vsock")) {
      return;
    }

    using var cancelled = new CancellationTokenSource(TimeSpan.FromSeconds(1));
    var elapsed = Stopwatch.StartNew();
    var error = Record.Exception(() =>
    {
      using var socket = VsockConnector.Connect(44987, TimeSpan.FromMilliseconds(500), cancelled.Token);
    });

    Assert.NotNull(error);
    Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2));
  }
}
