// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using Windows.Security.Credentials;
using PinentryForWindows.Configuration;
using PinentryForWindows.Platform.Windows;

namespace PinentryForWindows.Cli;

internal static class DiagnosticCommand {
  public static async Task<int> RunAsync(CancellationToken ct = default) {
    Console.WriteLine("PinentryForWindows diagnostics");
    Console.WriteLine(new string('=', 32));
    Console.WriteLine();

    var failures = 0;

    failures += await CheckWindowsHelloAsync(ct);
    failures += CheckPasswordVault();
    failures += CheckLogDirectory();
    failures += CheckSettingsFile();
    failures += await CheckGnuPgConfigurationAsync(ct);

    Console.WriteLine();
    Console.WriteLine(failures == 0
      ? "All checks passed."
      : $"{failures} check(s) failed.");

    return failures > 0 ? 1 : 0;
  }

  private static async Task<int> CheckWindowsHelloAsync(CancellationToken ct) {
    try {
      var supported = await KeyCredentialManager.IsSupportedAsync().AsTask(ct);
      if (supported) {
        Ok("Windows Hello: supported");
        return 0;
      }

      Warn("Windows Hello: unavailable — cache reads will always fall back to passphrase prompt");
      return 0;
    }
    catch (Exception ex) {
      Warn($"Windows Hello: check failed ({ex.Message})");
      return 1;
    }
  }

  private static int CheckPasswordVault() {
    try {
      var keys = CredentialServiceAdmin.ListCacheKeys();
      Ok($"PasswordVault: accessible ({keys.Count} cached {(keys.Count == 1 ? "entry" : "entries")})");
      return 0;
    }
    catch (Exception ex) {
      Fail($"PasswordVault: not accessible — {ex.Message}");
      return 1;
    }
  }

  private static int CheckLogDirectory() {
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var logDir = Path.Combine(localAppData, "PinentryForWindows");
    var logFile = Path.Combine(logDir, "pinentry.log");

    try {
      Directory.CreateDirectory(logDir);
      var probe = Path.Combine(logDir, $".write-probe-{Guid.NewGuid():N}");
      File.WriteAllText(probe, string.Empty);
      File.Delete(probe);
      Ok($"Log directory: writable ({logFile})");
      return 0;
    }
    catch (Exception ex) {
      Fail($"Log directory: not writable — {ex.Message}");
      return 1;
    }
  }

  private static int CheckSettingsFile() {
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var settingsPath = Path.Combine(localAppData, "PinentryForWindows", "settings.json");

    if (File.Exists(settingsPath)) {
      Ok($"Settings: AllowUserCacheOptIn = {AppConfiguration.AllowUserCacheOptIn} ({settingsPath})");
      return 0;
    }

    Warn($"Settings: file not found, defaults in use ({settingsPath})");
    return 0;
  }

  private static async Task<int> CheckGnuPgConfigurationAsync(CancellationToken ct) {
    string homedir;

    try {
      var psi = new ProcessStartInfo {
        FileName = "gpgconf",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        CreateNoWindow = true
      };
      psi.ArgumentList.Add("--list-dirs");
      psi.ArgumentList.Add("homedir");

      using var proc = Process.Start(psi);
      if (proc is null) {
        Warn("GnuPG: gpgconf could not be started — skipping GnuPG check");
        return 0;
      }

      homedir = (await proc.StandardOutput.ReadToEndAsync(ct)).Trim()
        .Replace('/', Path.DirectorySeparatorChar);
      await proc.WaitForExitAsync(ct);
    }
    catch {
      Warn("GnuPG: gpgconf not found in PATH — skipping GnuPG check");
      return 0;
    }

    var agentConfPath = Path.Combine(homedir, "gpg-agent.conf");
    if (!File.Exists(agentConfPath)) {
      Warn($"GnuPG: gpg-agent.conf not found ({agentConfPath})");
      return 0;
    }

    var pinentryLine = (await File.ReadAllLinesAsync(agentConfPath, ct))
      .FirstOrDefault(l => l.TrimStart().StartsWith("pinentry-program", StringComparison.OrdinalIgnoreCase));

    if (pinentryLine is null) {
      Fail($"GnuPG: pinentry-program not set in gpg-agent.conf ({agentConfPath})");
      return 1;
    }

    var parts = pinentryLine.Split(' ', 2, StringSplitOptions.TrimEntries);
    var configured = parts.Length > 1 ? parts[1] : string.Empty;
    var thisExe = Environment.ProcessPath ?? string.Empty;

    if (string.Equals(configured, thisExe, StringComparison.OrdinalIgnoreCase)) {
      Ok($"GnuPG: pinentry-program = {configured}");
      return 0;
    }

    Warn($"GnuPG: pinentry-program points to a different executable (configured: {configured}, this: {thisExe})");
    return 0;
  }

  private static void Ok(string message)
    => Console.WriteLine($"  [OK]   {message}");

  private static void Warn(string message)
    => Console.WriteLine($"  [WARN] {message}");

  private static void Fail(string message)
    => Console.WriteLine($"  [FAIL] {message}");
}
