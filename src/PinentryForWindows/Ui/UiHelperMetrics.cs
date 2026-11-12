// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Security;

namespace PinentryForWindows.Ui;

internal static class UiHelperMetrics {
  public static void Record(string? path, long allocatedBytes, long peakWorkingSetBytes, long privateBytes) {
    if (string.IsNullOrWhiteSpace(path) ||
        allocatedBytes < 0 ||
        peakWorkingSetBytes < 0 ||
        privateBytes < 0) {
      return;
    }

    try {
      if (Path.IsPathFullyQualified(path)) {
        File.AppendAllText(path, $"{allocatedBytes},{peakWorkingSetBytes},{privateBytes}\n");
      }
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
                                    NotSupportedException or SecurityException) {
      // Diagnostics must not change the result of a credential prompt.
    }
  }
}
