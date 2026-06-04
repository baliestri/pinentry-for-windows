// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.InteropServices;
using PinentryForWindows.Services;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;

namespace PinentryForWindows.Platform.Windows;

internal sealed class DialogService : IDialogService {
  private const int OK_BUTTON_ID = 1;
  private const int CANCEL_BUTTON_ID = 2;

  /// <inheritdoc />
  public void ShowMessage(string title, string message)
    => _ = ShowMessage(title, message, DialogIcon.Information);

  /// <inheritdoc />
  public void ShowWarning(string title, string message)
    => _ = ShowMessage(title, message, DialogIcon.Warning);

  /// <inheritdoc />
  public bool Confirm(string title, string message, string? okButtonText = null, string? cancelButtonText = null) {
    var okText = !string.IsNullOrWhiteSpace(okButtonText) ? okButtonText : "OK";
    var cancelText = !string.IsNullOrWhiteSpace(cancelButtonText) ? cancelButtonText : "Cancel";

    return ShowConfirm(title, message, okText, cancelText) == OK_BUTTON_ID;
  }

  private static unsafe int ShowMessage(string title, string message, DialogIcon icon) {
    fixed (char* titlePtr = title)
    fixed (char* messagePtr = message) {
      var config = CreateConfig(titlePtr, messagePtr, icon);
      config.dwCommonButtons = TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON;

      PInvoke.TaskDialogIndirect(in config, out var selectedButton, out _, out _).ThrowOnFailure();

      return selectedButton;
    }
  }

  private static unsafe int ShowConfirm(string title, string message, string okButtonText, string cancelButtonText) {
    fixed (char* titlePtr = title)
    fixed (char* messagePtr = message)
    fixed (char* okButtonTextPtr = okButtonText)
    fixed (char* cancelButtonTextPtr = cancelButtonText) {
      var buttons = stackalloc TASKDIALOG_BUTTON[2];
      buttons[0] = new TASKDIALOG_BUTTON {
        nButtonID = OK_BUTTON_ID,
        pszButtonText = new PCWSTR(okButtonTextPtr)
      };
      buttons[1] = new TASKDIALOG_BUTTON {
        nButtonID = CANCEL_BUTTON_ID,
        pszButtonText = new PCWSTR(cancelButtonTextPtr)
      };

      var config = CreateConfig(titlePtr, messagePtr, DialogIcon.Information);
      config.dwFlags = TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION;
      config.cButtons = 2;
      config.pButtons = buttons;
      config.nDefaultButton = OK_BUTTON_ID;

      PInvoke.TaskDialogIndirect(in config, out var selectedButton, out _, out _).ThrowOnFailure();

      return selectedButton;
    }
  }

  private static unsafe TASKDIALOGCONFIG CreateConfig(char* title, char* message, DialogIcon icon) {
    var config = new TASKDIALOGCONFIG {
      cbSize = (uint)Marshal.SizeOf<TASKDIALOGCONFIG>(),
      pszWindowTitle = new PCWSTR(title),
      pszContent = new PCWSTR(message)
    };

    config.Anonymous1.pszMainIcon = icon switch {
      DialogIcon.Warning => PInvoke.TD_WARNING_ICON,
      _ => PInvoke.TD_INFORMATION_ICON
    };

    return config;
  }

  private enum DialogIcon {
    Information,
    Warning
  }
}
