// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using PinentryForWindows.Cache;

namespace PinentryForWindows.Cli;

internal static class PinentryDiagnostics {
  public static async Task<int> RunAsync(CancellationToken ct) {
    Console.WriteLine("Pinentry for Windows diagnostics");
    Console.WriteLine("  [OK] Assuan pinentry executable available");
    Console.WriteLine(new NativeWebAuthnPrf().IsAvailable
      ? "  [OK] Windows Hello platform authenticator available (PRF verified only when used)"
      : "  [WARN] Windows Hello platform authenticator unavailable; persistent cache disabled");
    Console.WriteLine($"  [OK] Cache data directory: {PinentryPaths.DataDirectory}");

    await CheckGnuPgAsync(ct).ConfigureAwait(false);

    return 0;
  }

  private static async Task CheckGnuPgAsync(CancellationToken ct) {
    var start = new ProcessStartInfo("gpgconf") {
      UseShellExecute = false,
      CreateNoWindow = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true
    };
    start.ArgumentList.Add("--list-dirs");
    start.ArgumentList.Add("homedir");

    Process? process = null;
    try {
      process = Process.Start(start);
      if (process is null) {
        throw new InvalidOperationException("gpgconf could not start.");
      }

      using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
      deadline.CancelAfter(TimeSpan.FromSeconds(5));
      var stdout = process.StandardOutput.ReadToEndAsync(deadline.Token);
      var stderr = process.StandardError.ReadToEndAsync(deadline.Token);
      await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
      await stderr.ConfigureAwait(false);
      if (process.ExitCode != 0) {
        throw new InvalidOperationException("gpgconf failed.");
      }

      var home = Uri.UnescapeDataString((await stdout.ConfigureAwait(false)).Trim())
        .Replace('/', Path.DirectorySeparatorChar);
      await ReportConfiguredPinentryAsync(home, ct).ConfigureAwait(false);
    }
    catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested) {
      Console.WriteLine("  [WARN] GnuPG agent configuration could not be checked");
    }
    finally {
      if (process is not null) {
        try {
          if (!process.HasExited) {
            process.Kill(true);
          }
        }
        catch (InvalidOperationException) {
          /* Process exited concurrently. */
        }

        process.Dispose();
      }
    }
  }

  private static async Task ReportConfiguredPinentryAsync(string home, CancellationToken ct) {
    var config = Path.Combine(home, "gpg-agent.conf");
    if (!File.Exists(config)) {
      Console.WriteLine("  [WARN] GnuPG agent configuration not found");
      return;
    }

    var line = (await File.ReadAllLinesAsync(config, ct).ConfigureAwait(false)).FirstOrDefault(static text =>
      text.TrimStart().StartsWith("pinentry-program ", StringComparison.OrdinalIgnoreCase));
    if (line is null) {
      Console.WriteLine("  [WARN] GnuPG agent does not specify a pinentry-program");
      return;
    }

    var configured = line.Trim().Split(' ', 2)[1].Trim().Trim('"').Replace('/', Path.DirectorySeparatorChar);
    var executable = Environment.ProcessPath ?? string.Empty;
    Console.WriteLine(string.Equals(Path.GetFullPath(configured), executable, StringComparison.OrdinalIgnoreCase)
      ? "  [OK] GnuPG agent points to this pinentry"
      : "  [WARN] GnuPG agent points to another pinentry");
  }
}
