// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Globalization;
using System.Security;

namespace PinentryForWindows.Ui;

internal static class PinentryLatency {
  public static void Record(long getPinTimestamp, long uiCallTimestamp)
    => Record(Environment.GetEnvironmentVariable("PFW_LATENCY_LOG"), getPinTimestamp, uiCallTimestamp);

  internal static void Record(string? path, long getPinTimestamp, long uiCallTimestamp) {
    if (string.IsNullOrWhiteSpace(path) ||
        getPinTimestamp <= 0 ||
        uiCallTimestamp < getPinTimestamp) {
      return;
    }

    try {
      if (!Path.IsPathFullyQualified(path)) {
        return;
      }

      var milliseconds = Stopwatch.GetElapsedTime(getPinTimestamp, uiCallTimestamp).TotalMilliseconds;
      if (!double.IsFinite(milliseconds) ||
          milliseconds > TimeSpan.FromMinutes(5).TotalMilliseconds) {
        return;
      }

      File.AppendAllText(path, milliseconds.ToString("F3", CultureInfo.InvariantCulture) + "\n");
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or
                                    NotSupportedException or SecurityException) {
      // Best-effort measurement must never interfere with a password prompt.
    }
  }
}
