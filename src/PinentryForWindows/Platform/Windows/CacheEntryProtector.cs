// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Text;

namespace PinentryForWindows.Platform.Windows;

internal static class CacheEntryProtector {
  private const string PREFIX = "pfwv2:";

  internal static string Protect(string passphrase, string cacheKey) {
    var data = Encoding.UTF8.GetBytes(passphrase);
    try {
      var entropy = Encoding.UTF8.GetBytes(cacheKey);
      var blob = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
      return PREFIX + Convert.ToBase64String(blob);
    }
    finally {
      Array.Clear(data, 0, data.Length);
    }
  }

  internal static string? TryUnprotect(string stored, string cacheKey) {
    if (!stored.StartsWith(PREFIX, StringComparison.Ordinal)) {
      return null;
    }

    byte[]? data = null;
    try {
      var blob = Convert.FromBase64String(stored[PREFIX.Length..]);
      var entropy = Encoding.UTF8.GetBytes(cacheKey);
      data = ProtectedData.Unprotect(blob, entropy, DataProtectionScope.CurrentUser);
      return Encoding.UTF8.GetString(data);
    }
    catch {
      return null;
    }
    finally {
      if (data is not null) {
        Array.Clear(data, 0, data.Length);
      }
    }
  }
}
