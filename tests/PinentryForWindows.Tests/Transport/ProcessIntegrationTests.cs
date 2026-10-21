// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Text;
using PinentryForWindows.Broker;

namespace PinentryForWindows.Tests.Transport;

public sealed class ProcessIntegrationTests {
  [Fact]
  public async Task Local_process_accepts_mixed_utf8_and_native_encoded_gpg_labels() {
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var nativeEncoding = Encoding.GetEncoding(0);
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
                        ?? throw new InvalidOperationException("Test build configuration could not be determined.");
    var executable = Path.Combine(root, "src", "PinentryForWindows", "bin", configuration,
      "net10.0-windows10.0.26100.0", "pfw.exe");
    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false,
      WorkingDirectory = root,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      Environment = {
        ["PFW_VERBOSE"] = "0"
      }
    };
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start pinentry.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var commands = new[] {
      nativeEncoding.GetBytes("OPTION default-prompt=Code personnel\u00a0:\n"),
      nativeEncoding.GetBytes("OPTION default-tt-visi=Rendre la phrase secrète visible\n"),
      Encoding.UTF8.GetBytes("SETDESC Veuillez déverrouiller la clef OpenPGP%0Acréée en 2026\n"),
      nativeEncoding.GetBytes("SETPROMPT Phrase secrète\u00a0:\n"),
      Encoding.ASCII.GetBytes("BYE\n")
    };

    try {
      Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));
      foreach (var command in commands) {
        await process.StandardInput.BaseStream.WriteAsync(command, deadline.Token);
        await process.StandardInput.BaseStream.FlushAsync(deadline.Token);
        Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));
      }

      await process.WaitForExitAsync(deadline.Token);
      Assert.Equal(0, process.ExitCode);
      Assert.Equal(string.Empty, await process.StandardError.ReadToEndAsync(deadline.Token));
    }
    finally {
      if (!process.HasExited) {
        process.Kill(true);
        await process.WaitForExitAsync(CancellationToken.None);
      }
    }
  }

  [Fact]
  public async Task Local_process_handles_assuan_without_text_reencoding() {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
                        ?? throw new InvalidOperationException("Test build configuration could not be determined.");
    var executable = Path.Combine(root, "src", "PinentryForWindows", "bin", configuration,
      "net10.0-windows10.0.26100.0", "pfw.exe");
    Assert.True(File.Exists(executable), "Build the Windows executable before running process tests.");

    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false,
      WorkingDirectory = root,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      Environment = {
        ["PFW_VERBOSE"] = "0"
      }
    };
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start pinentry.");

    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));
    await process.StandardInput.WriteLineAsync("SETTITLE Test%20Pinentry");
    Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));
    await process.StandardInput.WriteLineAsync("GETINFO version");
    Assert.Equal("D " + typeof(BrokerIdentity).Assembly.GetName().Version,
      await process.StandardOutput.ReadLineAsync(deadline.Token));
    Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));
    await process.StandardInput.WriteLineAsync("GETINFO flavor");
    Assert.Equal("D pinentry-for-windows", await process.StandardOutput.ReadLineAsync(deadline.Token));
    Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));
    await process.StandardInput.WriteLineAsync("BYE");
    Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));

    await process.WaitForExitAsync(deadline.Token);
    Assert.Equal(0, process.ExitCode);
    Assert.Equal(string.Empty, await process.StandardError.ReadToEndAsync(deadline.Token));
  }

  [Fact]
  public async Task Verbose_local_session_writes_only_to_stderr() {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
                        ?? throw new InvalidOperationException("Test build configuration could not be determined.");
    var executable = Path.Combine(root, "src", "PinentryForWindows", "bin", configuration,
      "net10.0-windows10.0.26100.0", "pfw.exe");
    var start = new ProcessStartInfo(executable) {
      UseShellExecute = false,
      RedirectStandardInput = true,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      Environment = {
        ["PFW_VERBOSE"] = "1"
      }
    };
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start pinentry.");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));

    Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));
    await process.StandardInput.WriteLineAsync("GETINFO flavor");
    Assert.Equal("D pinentry-for-windows", await process.StandardOutput.ReadLineAsync(deadline.Token));
    Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));
    await process.StandardInput.WriteLineAsync("BYE");
    Assert.StartsWith("OK", await process.StandardOutput.ReadLineAsync(deadline.Token));
    await process.WaitForExitAsync(deadline.Token);

    Assert.Equal(0, process.ExitCode);
    var stderr = await process.StandardError.ReadToEndAsync(deadline.Token);
    Assert.Contains("[pfw:windows] local session started", stderr);
    Assert.DoesNotContain("GETINFO flavor", stderr);
    Assert.Equal(string.Empty, await process.StandardOutput.ReadToEndAsync(deadline.Token));
  }
}
