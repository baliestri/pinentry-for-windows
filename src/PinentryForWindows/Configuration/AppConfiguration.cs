// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.Json;

namespace PinentryForWindows.Configuration;

internal static class AppConfiguration {
  public static bool AllowUserCacheOptIn { get; internal set; }

  public static void Load() {
    var path = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "PinentryForWindows",
      "settings.json");

    if (!File.Exists(path)) {
      return;
    }

    try {
      using var doc = JsonDocument.Parse(File.ReadAllText(path));
      if (doc.RootElement.TryGetProperty("AllowUserCacheOptIn", out var prop)) {
        AllowUserCacheOptIn = prop.GetBoolean();
      }
    }
    catch {
      // Best-effort; invalid or unreadable settings file leaves defaults in place.
    }
  }
}
