// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Logging;

namespace PinentryForWindows.Logging;

internal sealed class FileLogger(string filePath) : IAssuanLogger {
  private static readonly IDisposable _nullScope = new NoopScope();
  private readonly Lock _gate = new();

  /// <inheritdoc />
  public IDisposable BeginScope<TState>(TState state) where TState : notnull
    => _nullScope;

  /// <inheritdoc />
  public bool IsEnabled(AssuanLogLevel logLevel)
    => logLevel >= AssuanLogLevel.Debug && logLevel is not AssuanLogLevel.None;

  /// <inheritdoc />
  public void Log<TState>(AssuanLogLevel logLevel, AssuanEventId eventId, TState state, Exception? exception,
  Func<TState, Exception?, string> formatter) {
    if (!IsEnabled(logLevel)) {
      return;
    }

    try {
      var directoryPath = Path.GetDirectoryName(filePath);
      if (!string.IsNullOrWhiteSpace(directoryPath)) {
        Directory.CreateDirectory(directoryPath);
      }

      var message = NormalizeLine(formatter(state, exception));
      var exceptionText = exception is null ? string.Empty : $" exception=\"{NormalizeLine(exception.ToString())}\"";
      var line = $"{DateTimeOffset.Now:O} [{logLevel}] {eventId.Id}:{eventId.Name} {message}{exceptionText}";

      lock (_gate) {
        File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
      }
    }
    catch {
      // Pinentry must never fail because diagnostic logging is unavailable.
    }
  }

  public static FileLogger CreateDefault() {
    var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var filePath = Path.Combine(localAppData, "PinentryForWindows", "pinentry.log");

    return new FileLogger(filePath);
  }

  private static string NormalizeLine(string value)
    => value
      .Replace("\r\n", "\\n", StringComparison.Ordinal)
      .Replace("\n", "\\n", StringComparison.Ordinal)
      .Replace("\r", "\\n", StringComparison.Ordinal);

  private sealed class NoopScope : IDisposable {
    public void Dispose() { }
  }
}
