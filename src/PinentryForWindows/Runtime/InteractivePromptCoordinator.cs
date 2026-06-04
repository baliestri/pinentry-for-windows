// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Runtime;

internal static class InteractivePromptCoordinator {
  public static InteractiveCommandSignal? Current { get; private set; }

  public static InteractiveCommandSignal Begin() {
    var signal = new InteractiveCommandSignal();
    Current = signal;

    return signal;
  }

  public static void Complete(InteractiveCommandSignal signal) {
    signal.Set();

    if (ReferenceEquals(Current, signal)) {
      Current = null;
    }
  }

  public static void Clear()
    => Current = null;

  public static void Restore(InteractiveCommandSignal? signal)
    => Current = signal;
}
