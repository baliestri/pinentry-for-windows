// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Security.Cryptography;
using PinentryForWindows.Cache;

namespace PinentryForWindows.Tests.Cli;

public sealed class CliCompatibilityTests {
  private const string GRIP = "0123456789ABCDEF0123456789ABCDEF01234567";

  [Fact]
  public async Task Check_reports_current_capabilities_without_creating_a_cache_credential() {
    var directory = TestDirectory();
    try {
      var result = await RunAsync(directory, "--check");
      Assert.Equal(0, result.ExitCode);
      Assert.Contains("[OK]", result.StdOut);
      Assert.DoesNotContain("PasswordVault", result.StdOut);
      Assert.False(File.Exists(Path.Combine(directory, "cache", "hello.cred")));
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Clear_cache_without_argument_uses_the_current_data_directory() {
    var directory = TestDirectory();
    try {
      var result = await RunAsync(directory, "--clear-cache");
      Assert.Equal(0, result.ExitCode);
      Assert.Contains("cache", result.StdOut, StringComparison.OrdinalIgnoreCase);
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Unknown_command_explains_available_cli_commands() {
    var directory = TestDirectory();
    try {
      const string SENSITIVE_ARGUMENT = "sensitive-cli-argument";
      var result = await RunAsync(directory, SENSITIVE_ARGUMENT);
      Assert.NotEqual(0, result.ExitCode);
      Assert.Contains("Usage", result.StdErr);
      Assert.DoesNotContain(SENSITIVE_ARGUMENT, result.StdErr);
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Clear_specific_key_only_removes_the_local_windows_entry() {
    var directory = TestDirectory();
    try {
      var cache = new WindowsPrfCache(Path.Combine(directory, "cache"), new FakePrf());
      await cache.WriteAsync("windows", GRIP, "local"u8.ToArray(), CancellationToken.None);
      await cache.WriteAsync("wsl:archlinux", GRIP, "wsl"u8.ToArray(), CancellationToken.None);

      var result = await RunAsync(directory, "--clear-cache", "n/" + GRIP);
      Assert.Equal(0, result.ExitCode);
      Assert.Null(await cache.ReadAsync("windows", GRIP, CancellationToken.None));
      using var other = await cache.ReadAsync("wsl:archlinux", GRIP, CancellationToken.None);
      Assert.NotNull(other);
      Assert.True(other.Expose().SequenceEqual("wsl"u8));
    }
    finally { Directory.Delete(directory, true); }
  }

  private static string TestDirectory() {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var directory = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    return directory;
  }

  private static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string directory,
    params string[] args) {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
                        ?? throw new InvalidOperationException("Test build configuration could not be determined.");
    var executable = Path.Combine(root, "src", "PinentryForWindows", "bin", configuration,
      "net10.0-windows10.0.26100.0", "pfw.exe");
    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      Environment = {
        ["PFW_DATA_DIR"] = directory,
        ["GNUPGHOME"] = directory
      }
    };
    foreach (var arg in args) start.ArgumentList.Add(arg);

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start pfw.");
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
    var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
    return (process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
  }

  private sealed class FakePrf : IPrfProvider {
    public bool IsAvailable => true;

    public ValueTask<byte[]> CreateCredentialAsync(CancellationToken ct)
      => ValueTask.FromResult(new byte[] { 1, 2, 3 });

    public ValueTask<byte[]> DeriveAsync(ReadOnlyMemory<byte> credentialId,
      ReadOnlyMemory<byte> salt, CancellationToken ct)
      => ValueTask.FromResult(HMACSHA256.HashData(credentialId.Span, salt.Span));

    public ValueTask DeleteCredentialAsync(ReadOnlyMemory<byte> credentialId, CancellationToken ct)
      => ValueTask.CompletedTask;
  }
}
