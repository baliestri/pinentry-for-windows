// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Ui;

internal static class UiGate {
  private const string MUTEX_NAME = @"Local\PinentryForWindows.Dialog.v2";

  public static T Run<T>(Func<T> action, CancellationToken ct) {
    using var mutex = new Mutex(false, MUTEX_NAME);
    var acquired = false;
    try {
      try {
        var index = WaitHandle.WaitAny([mutex, ct.WaitHandle]);
        if (index != 0) {
          throw new OperationCanceledException(ct);
        }

        acquired = true;
      }
      catch (AbandonedMutexException) {
        acquired = true;
      }

      ct.ThrowIfCancellationRequested();
      return action();
    }
    finally {
      if (acquired) {
        mutex.ReleaseMutex();
      }
    }
  }
}
