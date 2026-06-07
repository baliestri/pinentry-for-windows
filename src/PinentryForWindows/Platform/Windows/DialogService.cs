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
  private static readonly PFTASKDIALOGCALLBACK _callback = TaskDialogCallback;

  /// <inheritdoc />
  public DialogResponse ShowMessage(string title, string message, int timeoutSeconds = 0)
    => ShowMessage(title, message, DialogIcon.Information, timeoutSeconds) == DialogResult.TimedOut
      ? DialogResponse.TimedOut
      : DialogResponse.Accepted;

  /// <inheritdoc />
  public void ShowWarning(string title, string message)
    => _ = ShowMessage(title, message, DialogIcon.Warning, 0);

  /// <inheritdoc />
  public DialogResponse Confirm(string title, string message, string? okButtonText = null, string? cancelButtonText = null, int timeoutSeconds = 0) {
    var okText = !string.IsNullOrWhiteSpace(okButtonText) ? okButtonText : "OK";
    var cancelText = !string.IsNullOrWhiteSpace(cancelButtonText) ? cancelButtonText : "Cancel";

    var result = ShowConfirm(title, message, okText, cancelText, timeoutSeconds);
    return result switch {
      DialogResult.Accepted => DialogResponse.Accepted,
      DialogResult.TimedOut => DialogResponse.TimedOut,
      var _ => DialogResponse.Cancelled
    };
  }

  private static unsafe DialogResult ShowMessage(string title, string message, DialogIcon icon, int timeoutSeconds) {
    fixed (char* titlePtr = title)
    fixed (char* messagePtr = message) {
      var config = CreateConfig(titlePtr, messagePtr, icon);
      config.dwCommonButtons = TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON;

      _ = ShowTaskDialog(ref config, timeoutSeconds, OK_BUTTON_ID, out var timedOut);

      return timedOut ? DialogResult.TimedOut : DialogResult.Accepted;
    }
  }

  private static unsafe DialogResult ShowConfirm(string title, string message, string okButtonText, string cancelButtonText, int timeoutSeconds) {
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

      var selectedButton = ShowTaskDialog(ref config, timeoutSeconds, CANCEL_BUTTON_ID, out var timedOut);

      if (timedOut) {
        return DialogResult.TimedOut;
      }

      return selectedButton == OK_BUTTON_ID ? DialogResult.Accepted : DialogResult.Cancelled;
    }
  }

  private static int ShowTaskDialog(ref TASKDIALOGCONFIG config, int timeoutSeconds, int timeoutButtonId, out bool timedOut) {
    if (timeoutSeconds <= 0) {
      PInvoke.TaskDialogIndirect(in config, out var selectedButton, out _, out _).ThrowOnFailure();
      timedOut = false;
      return selectedButton;
    }

    var timeoutState = new TimeoutState(timeoutSeconds, timeoutButtonId);
    var timeoutStateHandle = GCHandle.Alloc(timeoutState);
    try {
      config.dwFlags |= TASKDIALOG_FLAGS.TDF_CALLBACK_TIMER;
      config.pfCallback = _callback;
      config.lpCallbackData = GCHandle.ToIntPtr(timeoutStateHandle);

      PInvoke.TaskDialogIndirect(in config, out var selectedButton, out _, out _).ThrowOnFailure();
      timedOut = timeoutState.TimedOut;
      return selectedButton;
    }
    finally {
      timeoutStateHandle.Free();
    }
  }

  private static HRESULT TaskDialogCallback(HWND hwnd, TASKDIALOG_NOTIFICATIONS msg, WPARAM wParam, LPARAM lParam, nint lpRefData) {
    if (msg != TASKDIALOG_NOTIFICATIONS.TDN_TIMER ||
        GCHandle.FromIntPtr(lpRefData).Target is not TimeoutState timeoutState ||
        (long)wParam.Value < timeoutState.TimeoutMilliseconds) {
      return new HRESULT(0);
    }

    timeoutState.TimedOut = true;
    _ = PInvoke.SendMessage(hwnd, (uint)TASKDIALOG_MESSAGES.TDM_CLICK_BUTTON, new WPARAM((nuint)timeoutState.TimeoutButtonId), default);

    return new HRESULT(0);
  }

  private static unsafe TASKDIALOGCONFIG CreateConfig(char* title, char* message, DialogIcon icon) {
    var config = new TASKDIALOGCONFIG {
      cbSize = (uint)Marshal.SizeOf<TASKDIALOGCONFIG>(),
      pszWindowTitle = new PCWSTR(title),
      pszContent = new PCWSTR(message)
    };

    config.Anonymous1.pszMainIcon = icon switch {
      DialogIcon.Warning => PInvoke.TD_WARNING_ICON,
      var _ => PInvoke.TD_INFORMATION_ICON
    };

    return config;
  }

  private enum DialogIcon {
    Information,
    Warning
  }

  private enum DialogResult {
    Accepted,
    Cancelled,
    TimedOut
  }

  private sealed class TimeoutState(int timeoutSeconds, int timeoutButtonId) {
    public long TimeoutMilliseconds { get; } = Math.Min((long)timeoutSeconds * 1000, uint.MaxValue);
    public int TimeoutButtonId { get; } = timeoutButtonId;
    public bool TimedOut { get; set; }
  }
}
