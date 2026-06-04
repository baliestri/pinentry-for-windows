// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Text;

namespace PinentryForWindows.Tests.TestSupport;

internal sealed class PinentryProcessSession : IAsyncDisposable {
  private readonly Process _process;

  private PinentryProcessSession(Process process)
    => _process = process;

  public static async Task<PinentryProcessSession> StartAsync() {
    var executablePath = FindExecutablePath();
    var process = new Process {
      StartInfo = new ProcessStartInfo {
        FileName = executablePath,
        WorkingDirectory = Path.GetDirectoryName(executablePath)!,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardInputEncoding = new UTF8Encoding(false),
        StandardOutputEncoding = new UTF8Encoding(false),
        StandardErrorEncoding = new UTF8Encoding(false)
      },
      EnableRaisingEvents = true
    };

    process.Start().ShouldBeTrue();
    _ = process.StandardError.ReadToEndAsync();

    var session = new PinentryProcessSession(process);
    var banner = await session.ReadResponseAsync();
    banner.Terminal.ShouldStartWith("OK");

    return session;
  }

  public async Task<AssuanProcessResponse> SendAsync(string commandLine) {
    await _process.StandardInput.WriteLineAsync(commandLine);
    await _process.StandardInput.FlushAsync();

    return await ReadResponseAsync();
  }

  public async ValueTask DisposeAsync() {
    if (!_process.HasExited) {
      try {
        await SendAsync("BYE");
      }
      catch {
        // Cleanup must not hide the original test failure.
      }
    }

    if (!_process.HasExited) {
      _process.Kill(entireProcessTree: true);
    }

    await _process.WaitForExitAsync();
    _process.Dispose();
  }

  private async Task<AssuanProcessResponse> ReadResponseAsync() {
    var lines = new List<string>();
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    while (!cts.IsCancellationRequested) {
      var readTask = _process.StandardOutput.ReadLineAsync(cts.Token).AsTask();
      var line = await readTask;

      line.ShouldNotBeNull("pinentry closed stdout before a terminal Assuan response");
      lines.Add(line);

      if (line.StartsWith("OK", StringComparison.Ordinal) ||
          line.StartsWith("ERR", StringComparison.Ordinal)) {
        return new AssuanProcessResponse(lines.ToArray(), line);
      }
    }

    throw new TimeoutException($"Timed out waiting for Assuan response. Lines: {string.Join(" | ", lines)}");
  }

  private static string FindExecutablePath() {
    var root = FindRepositoryRoot();
    var projectBin = Path.Combine(root, "src", "PinentryForWindows", "bin");
    var candidates = Directory.Exists(projectBin)
      ? Directory.EnumerateFiles(projectBin, "PinentryForWindows.exe", SearchOption.AllDirectories)
        .Where(path => path.Contains($"{Path.DirectorySeparatorChar}win-x64{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .ToArray()
      : [];

    if (candidates.FirstOrDefault() is { } projectCandidate) {
      return projectCandidate;
    }

    var outputCandidate = Path.Combine(AppContext.BaseDirectory, "PinentryForWindows.exe");
    if (File.Exists(outputCandidate)) {
      return outputCandidate;
    }

    throw new FileNotFoundException("PinentryForWindows.exe was not found. Build the solution before running integration tests.");
  }

  private static string FindRepositoryRoot() {
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null) {
      if (File.Exists(Path.Combine(directory.FullName, "PinentryForWindows.slnx")) ||
          File.Exists(Path.Combine(directory.FullName, "PinentryForWindows.sln"))) {
        return directory.FullName;
      }

      directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
  }
}

internal sealed record AssuanProcessResponse(IReadOnlyList<string> Lines, string Terminal) {
  public string Text => string.Join("\n", Lines);
}
