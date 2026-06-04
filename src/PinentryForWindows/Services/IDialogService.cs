// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Services;

internal interface IDialogService {
  void ShowMessage(string title, string message);
  void ShowWarning(string title, string message);
  bool Confirm(string title, string message, string? okButtonText = null, string? cancelButtonText = null);
}
