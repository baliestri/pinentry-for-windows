// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace PinentryForWindows.Relay.Tests;

public sealed class VerboseLoggingTests {
  [Fact]
  public async Task Verbose_relay_cli_writes_only_fixed_events_to_stderr() {
    if (!OperatingSystem.IsLinux()) {
      return;
    }

    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
                        ?? throw new InvalidOperationException("Test build configuration could not be determined.");
    var executable = Path.Combine(root, "src", "PinentryForWindows.Relay", "bin", configuration,
      "net10.0", "pfw-relay");
    var unusedDirectory = Path.Combine(root, "artifacts", "tests", "relay-verbose-unused");
    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };
    start.ArgumentList.Add("--remove-identity");
    start.ArgumentList.Add(unusedDirectory);
    start.Environment["PFW_VERBOSE"] = "1";
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start relay.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
    var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
    await process.WaitForExitAsync(deadline.Token);

    Assert.Equal(0, process.ExitCode);
    Assert.Equal("Relay identity removed.\n", (await stdout).Replace("\r\n", "\n"));
    Assert.Contains("[pfw:relay] command started", await stderr);
    Assert.DoesNotContain(unusedDirectory, await stderr);
  }
}
