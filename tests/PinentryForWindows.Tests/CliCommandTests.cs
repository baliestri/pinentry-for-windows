// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace PinentryForWindows.Tests;

public sealed class CliCommandTests {
  [Fact]
  public async Task Check_command_exits_zero() {
    var (exitCode, stdout, _) = await RunCliAsync("--check");

    exitCode.ShouldBe(0);
    stdout.ShouldContain("[OK]");
  }

  [Fact]
  public async Task Check_command_stdout_never_contains_dpapi_blob() {
    var (_, stdout, _) = await RunCliAsync("--check");

    stdout.ShouldNotContain("pfwv2:");
  }

  [Fact]
  public async Task ClearCache_command_exits_zero_when_no_entries() {
    var (exitCode, _, _) = await RunCliAsync("--clear-cache");

    exitCode.ShouldBe(0);
  }

  [Fact]
  public async Task ClearCache_specific_key_exits_zero_when_key_absent() {
    var (exitCode, stdout, _) = await RunCliAsync("--clear-cache", "nonexistent-key-for-test");

    exitCode.ShouldBe(0);
    stdout.ShouldContain("No cache entry found");
  }

  [Fact]
  public async Task Unknown_argument_exits_nonzero() {
    var (exitCode, _, stderr) = await RunCliAsync("--unknown-argument");

    exitCode.ShouldNotBe(0);
    stderr.ShouldContain("Unknown argument");
  }

  private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(params string[] args) {
    var exe = FindExecutablePath();

    var psi = new ProcessStartInfo(exe) {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    foreach (var arg in args) {
      psi.ArgumentList.Add(arg);
    }

    using var proc = Process.Start(psi)!;
    var stdoutTask = proc.StandardOutput.ReadToEndAsync();
    var stderrTask = proc.StandardError.ReadToEndAsync();

    await proc.WaitForExitAsync();

    return (proc.ExitCode, await stdoutTask, await stderrTask);
  }

  private static string FindExecutablePath() {
    var path = Path.Combine(AppContext.BaseDirectory, "PinentryForWindows.exe");
    return File.Exists(path)
      ? path
      : throw new FileNotFoundException("PinentryForWindows.exe was not found. Build the solution before running tests.");
  }
}
