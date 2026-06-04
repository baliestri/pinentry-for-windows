// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Logging;
using PinentryForWindows.Logging;

namespace PinentryForWindows.Tests;

public sealed class FileLoggerTests : IDisposable {
  private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "PinentryForWindows.Tests", Guid.NewGuid().ToString("N"));

  public void Dispose() {
    if (Directory.Exists(_tempRoot)) {
      Directory.Delete(_tempRoot, recursive: true);
    }
  }

  [Fact]
  public void IsEnabled_enables_debug_and_above_except_none() {
    var logger = new FileLogger(Path.Combine(_tempRoot, "pinentry.log"));

    logger.IsEnabled(AssuanLogLevel.Trace).ShouldBeFalse();
    logger.IsEnabled(AssuanLogLevel.Debug).ShouldBeTrue();
    logger.IsEnabled(AssuanLogLevel.Information).ShouldBeTrue();
    logger.IsEnabled(AssuanLogLevel.None).ShouldBeFalse();
  }

  [Fact]
  public void Log_creates_directory_normalizes_lines_and_includes_exception() {
    var logPath = Path.Combine(_tempRoot, "nested", "pinentry.log");
    var logger = new FileLogger(logPath);

    logger.Log(
      AssuanLogLevel.Warning,
      new AssuanEventId(10, "TestEvent"),
      "line 1\r\nline 2",
      new InvalidOperationException("boom\r\nnext"),
      (state, _) => state);

    var text = File.ReadAllText(logPath);
    text.ShouldContain("[Warning] 10:TestEvent line 1\\nline 2");
    text.ShouldContain("exception=\"System.InvalidOperationException: boom\\nnext");
  }

  [Fact]
  public void Log_ignores_disabled_levels() {
    var logPath = Path.Combine(_tempRoot, "pinentry.log");
    var logger = new FileLogger(logPath);

    logger.Log(AssuanLogLevel.Trace, new AssuanEventId(1, "Trace"), "ignored", null, (state, _) => state);

    File.Exists(logPath).ShouldBeFalse();
  }

  [Fact]
  public void Log_never_throws_when_target_cannot_be_written() {
    Directory.CreateDirectory(_tempRoot);
    var logger = new FileLogger(_tempRoot);

    Should.NotThrow(() => logger.Log(
      AssuanLogLevel.Error,
      new AssuanEventId(1, "Error"),
      "message",
      null,
      (state, _) => state));
  }
}
