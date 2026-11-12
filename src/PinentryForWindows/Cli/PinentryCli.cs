// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Cache;

namespace PinentryForWindows.Cli;

internal static class PinentryCli {
  public static async Task<int> RunAsync(string[] args, CancellationToken ct) {
    if (args is ["--check"]) {
      return await PinentryDiagnostics.RunAsync(ct).ConfigureAwait(false);
    }

    if (args.Length is 1 or 2 &&
        string.Equals(args[0], "--clear-cache", StringComparison.Ordinal)) {
      var credentialRemoved = true;
      if (args.Length == 1) {
        credentialRemoved = await PinentryPaths.CreateCache().ClearAllAsync(ct).ConfigureAwait(false);
      }
      else if (TryGrip(args[1], out var grip)) {
        await PinentryPaths.CreateCache().RemoveAsync("windows", grip, ct).ConfigureAwait(false);
      }
      else if (LooksLikeDirectory(args[1])) {
        credentialRemoved = await new WindowsPrfCache(Path.Combine(Path.GetFullPath(args[1]), "cache"),
          new NativeWebAuthnPrf()).ClearAllAsync(ct).ConfigureAwait(false);
      }
      else {
        Console.WriteLine("No cache entry found for the specified key.");
        return 0;
      }

      Console.WriteLine("Pinentry cache cleared (if present).");
      if (!credentialRemoved) {
        Console.WriteLine("The Windows Hello passkey could not be removed; if \"Pinentry for Windows\" is still " +
                          "listed under Settings > Accounts > Passkeys, delete it there.");
      }

      return 0;
    }

    if (args is ["--help" or "-h"]) {
      PrintUsage(Console.Out);
      return 0;
    }

    await Console.Error.WriteLineAsync("Unsupported command.").ConfigureAwait(false);
    PrintUsage(Console.Error);
    return 2;
  }

  private static bool TryGrip(string value, out string grip) {
    grip = value.Length == 42 && value[1] == '/' && value[0] is 'n' or 's' or 'u'
      ? value[2..]
      : value;
    if (grip.Length != 40 ||
        !grip.All(Uri.IsHexDigit)) {
      return false;
    }

    grip = grip.ToUpperInvariant();
    return true;
  }

  private static bool LooksLikeDirectory(string value)
    => Directory.Exists(value) || Path.IsPathFullyQualified(value) || value.StartsWith('.') || value.Contains('\\') ||
       value.Contains('/') &&
       !value.StartsWith("n/", StringComparison.Ordinal) &&
       !value.StartsWith("s/", StringComparison.Ordinal) &&
       !value.StartsWith("u/", StringComparison.Ordinal);

  private static void PrintUsage(TextWriter output) {
    output.WriteLine("Usage: pfw.exe [command]");
    output.WriteLine("  --check                 Show current pinentry diagnostics");
    output.WriteLine("  --clear-cache           Clear the current user's persistent cache");
    output.WriteLine("  --clear-cache <key>     Clear the local Windows software-key entry");
    output.WriteLine("  --clear-cache <dir>     Clear a cache in a specified data directory");
    output.WriteLine("  --init-broker <dir>     Create the broker identity");
    output.WriteLine("  --approve-client <dir> <cert> <origin>  Pair a WSL client");
    output.WriteLine("  --replace-client <dir> <cert> <origin>  Pair a renewed WSL client certificate");
    output.WriteLine("  --revoke-client <dir> <cert>          Revoke a WSL client");
    output.WriteLine("  --renew-broker <dir>    Replace the broker certificate, keeping paired clients");
    output.WriteLine("  --reset-broker <dir>    Remove broker pairing material");
    output.WriteLine("  --vm-id <distribution>  Find the running WSL VM");
    output.WriteLine("  --broker <dir>          Run the interactive Windows broker");
    output.WriteLine("  --help                  Show this help");
  }
}
