// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Cli;

internal static class CliDispatcher {
  internal static async Task<int> RunAsync(string[] args) {
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => {
      e.Cancel = true;
      cts.Cancel();
    };

    return args[0] switch {
      "--check" => await DiagnosticCommand.RunAsync(cts.Token),
      "--clear-cache" when args.Length == 1 => await ClearCacheCommand.RunAsync(null, cts.Token),
      "--clear-cache" when args.Length >= 2 => await ClearCacheCommand.RunAsync(args[1], cts.Token),
      var _ => PrintUsage(args[0])
    };

    static int PrintUsage(string unknown) {
      Console.Error.WriteLine($"Unknown argument: {unknown}");
      Console.Error.WriteLine("Usage:");
      Console.Error.WriteLine("  PinentryForWindows.exe                      Start Assuan server (normal mode)");
      Console.Error.WriteLine("  PinentryForWindows.exe --check              Run diagnostics");
      Console.Error.WriteLine("  PinentryForWindows.exe --clear-cache        Clear all cached passphrases");
      Console.Error.WriteLine("  PinentryForWindows.exe --clear-cache <key>  Clear specific cache entry");
      return 1;
    }
  }
}
