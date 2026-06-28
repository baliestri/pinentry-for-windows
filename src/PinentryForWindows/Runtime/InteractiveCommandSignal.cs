// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Runtime;

internal sealed class InteractiveCommandSignal {
  private readonly ManualResetEventSlim _event = new(false);
  private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

  /// <summary>
  ///   Signals that the interactive command has completed. This will unblock any threads waiting on <see cref="Wait" /> or <see cref="WaitAsync" />.
  /// </summary>
  public void Set() {
    _tcs.TrySetResult();
    _event.Set();
  }

  /// <summary>
  ///   Waits for the interactive command to complete. This will block the calling thread until <see cref="Set" /> is called.
  /// </summary>
  public void Wait()
    => _event.Wait();

  /// <summary>
  ///   Asynchronously waits for the interactive command to complete. This will return a task that completes when <see cref="Set" /> is called.
  /// </summary>
  /// <returns>A task that completes when the interactive command is signaled.</returns>
  public Task WaitAsync()
    => _tcs.Task;
}
