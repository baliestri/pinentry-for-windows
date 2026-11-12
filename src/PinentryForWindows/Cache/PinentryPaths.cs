// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Cache;

internal static class PinentryPaths {
  public static string DataDirectory {
    get
    {
      var configured = Environment.GetEnvironmentVariable("PFW_DATA_DIR");
      return string.IsNullOrWhiteSpace(configured)
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PinentryForWindows")
        : Path.GetFullPath(configured);
    }
  }

  public static WindowsPrfCache CreateCache()
    => new(Path.Combine(DataDirectory, "cache"), new NativeWebAuthnPrf());
}
