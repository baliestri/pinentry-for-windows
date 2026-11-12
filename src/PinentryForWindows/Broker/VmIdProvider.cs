// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Text;

namespace PinentryForWindows.Broker;

internal static class VmIdProvider {
  public static async Task<Guid?> TryGetAsync(string distribution, CancellationToken ct) {
    if (string.IsNullOrWhiteSpace(distribution) ||
        distribution.Length > 100 ||
        distribution.StartsWith('-') ||
        distribution.IndexOfAny(['\r', '\n', '\0']) >= 0) {
      throw new ArgumentException("Invalid distribution name.", nameof(distribution));
    }

    var running = await InvokeWslAsync(Encoding.Unicode, ct, "--list", "--running", "--quiet").ConfigureAwait(false);
    if (running is null ||
        !running.Replace("\0", string.Empty).Split(['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
          .Contains(distribution, StringComparer.OrdinalIgnoreCase)) {
      return null;
    }

    var result = await InvokeWslAsync(Encoding.UTF8, ct, "--distribution", distribution, "--", "wslinfo", "--vm-id")
      .ConfigureAwait(false);
    return Guid.TryParse(result?.Replace("\0", string.Empty).Trim(), out var vmId) ? vmId : null;
  }

  private static async Task<string?> InvokeWslAsync(Encoding encoding, CancellationToken ct,
  params string[] arguments) {
    // Never resolve wsl.exe through the application or current directory.
    var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "wsl.exe")) {
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      StandardOutputEncoding = encoding
    };

    foreach (var argument in arguments) {
      start.ArgumentList.Add(argument);
    }

    Process? started;
    lock (ChildProcessGate.Sync) { started = Process.Start(start); }

    using var process = started;
    if (process is null) {
      return null;
    }

    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
    deadline.CancelAfter(TimeSpan.FromSeconds(5));
    try {
      var output = await process.StandardOutput.ReadToEndAsync(deadline.Token).ConfigureAwait(false);
      await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
      return process.ExitCode == 0 && output.Length < 4096 ? output : null;
    }
    catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
      if (!process.HasExited) {
        process.Kill(true);
      }

      return null;
    }
  }
}
