// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace PinentryForWindows.Relay.Tests;

public sealed class VersionCommandTests {
  [Fact]
  public async Task Version_prints_only_the_relay_version_on_stdout() {
    if (!OperatingSystem.IsLinux()) {
      return;
    }

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
                        ?? throw new InvalidOperationException("Test build configuration could not be determined.");
    var executable = Path.Combine(root, "src", "PinentryForWindows.Relay", "bin", configuration,
      "net10.0", "pfw-relay");
    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };
    start.ArgumentList.Add("--version");
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start relay.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
    var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
    await process.WaitForExitAsync(deadline.Token);

    Assert.Equal(0, process.ExitCode);
    Assert.Matches(@"\Apfw-relay \d+\.\d+\.\d+\.\d+\n\z", await stdout);
    Assert.Empty(await stderr);
  }
}
