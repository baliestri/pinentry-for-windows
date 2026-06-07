// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Services;

internal enum DialogResponse {
  Accepted,
  Cancelled,
  TimedOut
}

internal interface IDialogService {
  DialogResponse ShowMessage(string title, string message, int timeoutSeconds = 0);
  void ShowWarning(string title, string message);
  DialogResponse Confirm(string title, string message, string? okButtonText = null, string? cancelButtonText = null, int timeoutSeconds = 0);
}
