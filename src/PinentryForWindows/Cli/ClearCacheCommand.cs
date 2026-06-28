// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Platform.Windows;

namespace PinentryForWindows.Cli;

internal static class ClearCacheCommand {
  public static Task<int> RunAsync(string? keyInfo, CancellationToken ct = default) {
    if (keyInfo is null) {
      var removed = CredentialServiceAdmin.ClearAllCache();
      Console.WriteLine(removed == 1
        ? "Removed 1 cached passphrase."
        : $"Removed {removed} cached passphrases.");

      return Task.FromResult(0);
    }

    var fullKey = SessionState.CachePrefix + keyInfo;
    var found = CredentialServiceAdmin.RemoveCacheKey(fullKey);
    Console.WriteLine(found
      ? $"Removed cache entry '{keyInfo}'."
      : $"No cache entry found for '{keyInfo}'.");

    return Task.FromResult(0);
  }
}
