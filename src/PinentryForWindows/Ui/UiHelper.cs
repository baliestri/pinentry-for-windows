// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.IO.Pipes;
using PinentryForWindows.Core.Contracts;
using PinentryForWindows.Diagnostics;

namespace PinentryForWindows.Ui;

internal static class UiHelper {
  public static async Task<int> RunAsync(string readHandle, string writeHandle) {
    await using var requests = new AnonymousPipeClientStream(PipeDirection.In, readHandle);
    await using var responses = new AnonymousPipeClientStream(PipeDirection.Out, writeHandle);
    var metricsPath = Environment.GetEnvironmentVariable("PFW_HELPER_METRICS_LOG");
    var measure = !string.IsNullOrWhiteSpace(metricsPath);
    var allocatedBefore = measure ? GC.GetTotalAllocatedBytes() : 0;
    try {
      var request = await UiWire.ReadRequestAsync(requests, CancellationToken.None);
      using var result = NativeCredentialPrompt.Show(request);
      if (result is null) {
        await UiWire.WriteCancelledAsync(responses, CancellationToken.None);
      }
      else {
        await UiWire.WriteResultAsync(responses, result.Bytes, result.Remember, CancellationToken.None);
      }

      return 0;
    }
    catch (Exception error) {
      VerboseLog.Write(VerboseEvent.HelperFailed);
      try {
        if (error is SecretTooLongException) {
          await UiWire.WriteTooLongAsync(responses, CancellationToken.None);
        }
        else {
          await UiWire.WriteErrorAsync(responses, CancellationToken.None);
        }
      }
      catch (Exception) {
        /* The parent has already closed the private pipe. */
      }

      return 1;
    }
    finally {
      if (measure) {
        try {
          using var process = Process.GetCurrentProcess();
          process.Refresh();
          UiHelperMetrics.Record(metricsPath, Math.Max(0, GC.GetTotalAllocatedBytes() - allocatedBefore),
            process.PeakWorkingSet64, process.PrivateMemorySize64);
        }
        catch (Exception) {
          /* Optional metrics must never affect the prompt result. */
        }
      }
    }
  }
}
