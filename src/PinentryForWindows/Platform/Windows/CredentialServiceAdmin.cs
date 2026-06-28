// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using Windows.Security.Credentials;

namespace PinentryForWindows.Platform.Windows;

internal static class CredentialServiceAdmin {
  private const string CACHE_KEY_PREFIX = "pfwcache:";

  internal static IReadOnlyList<string> ListCacheKeys() {
    try {
      return new PasswordVault().RetrieveAll()
        .Where(c => c.Resource.StartsWith(CACHE_KEY_PREFIX, StringComparison.Ordinal))
        .Select(c => c.Resource)
        .ToList();
    }
    catch {
      return [];
    }
  }

  internal static int ClearAllCache() {
    var vault = new PasswordVault();
    var removed = 0;

    foreach (var key in ListCacheKeys()) {
      try {
        foreach (var credential in vault.FindAllByResource(key)) {
          vault.Remove(credential);
          removed++;
        }
      }
      catch {
        // Ignore per-entry failures; continue clearing others.
      }
    }

    return removed;
  }

  internal static bool RemoveCacheKey(string fullKey) {
    try {
      var vault = new PasswordVault();
      var found = false;

      foreach (var credential in vault.FindAllByResource(fullKey)) {
        vault.Remove(credential);
        found = true;
      }

      return found;
    }
    catch {
      return false;
    }
  }
}
