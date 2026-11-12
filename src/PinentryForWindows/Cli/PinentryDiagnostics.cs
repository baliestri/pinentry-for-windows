// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using PinentryForWindows.Broker;
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
    ReportBridgeCertificates(new BrokerIdentity(Path.Combine(PinentryPaths.DataDirectory, "broker")), DateTime.UtcNow);

    await CheckGnuPgAsync(ct).ConfigureAwait(false);

    return 0;
  }

  private static void ReportBridgeCertificates(BrokerIdentity identity, DateTime now) {
    DateTime? broker;
    IReadOnlyList<(string Origin, DateTime Expiry)> clients;
    try {
      broker = identity.ServerCertificateExpiry();
      clients = identity.ClientCertificateExpiries();
    }
    catch (Exception error) when (error is IOException or CryptographicException or UnauthorizedAccessException) {
      Console.WriteLine("  [WARN] WSL bridge certificates could not be read");
      return;
    }

    if (broker is null) {
      return; // The WSL bridge is optional and not configured for this user.
    }

    ReportCertificate("WSL bridge broker certificate", broker.Value, now, "pfw.exe --renew-broker");
    foreach (var (origin, expiry) in clients)
      ReportCertificate($"WSL client certificate for {origin}", expiry, now, "pfw-relay --renew in that distribution");
  }

  private static void ReportCertificate(string subject, DateTime expiry, DateTime now, string remedy) {
    var date = expiry.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    if (CertificateLifetime.IsExpired(expiry, now)) {
      Console.WriteLine($"  [WARN] {subject} expired on {date}; renew it with {remedy}");
    }
    else if (CertificateLifetime.IsExpiringSoon(expiry, now)) {
      Console.WriteLine($"  [WARN] {subject} expires on {date}; renew it with {remedy}");
    }
    else {
      Console.WriteLine($"  [OK] {subject} valid until {date}");
    }
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
