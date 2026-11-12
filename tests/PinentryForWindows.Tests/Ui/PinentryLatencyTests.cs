// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Globalization;
using PinentryForWindows.Ui;

namespace PinentryForWindows.Tests.Ui;

public sealed class PinentryLatencyTests {
  [Fact]
  public void Opt_in_file_contains_only_a_numeric_elapsed_time() {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var directory = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      var path = Path.Combine(directory, "latency.txt");
      var started = Stopwatch.GetTimestamp();
      PinentryLatency.Record(path, started, started + Stopwatch.Frequency / 10);

      var line = File.ReadAllText(path).Trim();
      Assert.True(double.TryParse(line, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture,
        out var milliseconds));
      Assert.InRange(milliseconds, 99, 101);
      Assert.DoesNotContain("GETPIN", line);
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public void Helper_resource_sample_contains_only_numeric_allocations_and_memory() {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var directory = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      var path = Path.Combine(directory, "helper.csv");
      UiHelperMetrics.Record(path, 4096, 12 * 1024 * 1024, 6 * 1024 * 1024);

      Assert.Equal("4096,12582912,6291456", File.ReadAllText(path).Trim());
    }
    finally { Directory.Delete(directory, true); }
  }
}
