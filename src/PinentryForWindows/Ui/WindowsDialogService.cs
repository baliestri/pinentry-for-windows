// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using PinentryForWindows.Core.Contracts;

namespace PinentryForWindows.Ui;

internal static class WindowsDialogService {
  private const int OK_BUTTON = 1;
  private const int CANCEL_BUTTON = 2;
  private readonly static PFTASKDIALOGCALLBACK _callback = OnNotification;

  public static unsafe bool Show(DialogRequest request, CancellationToken ct) {
    fixed (char* title = request.Title, message = request.Description,
           okLabel = request.OkLabel, cancelLabel = request.CancelLabel) {
      var config = new TASKDIALOGCONFIG {
        cbSize = (uint)Marshal.SizeOf<TASKDIALOGCONFIG>(),
        pszWindowTitle = new PCWSTR(title),
        pszContent = new PCWSTR(message),
        dwFlags = TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION | TASKDIALOG_FLAGS.TDF_CALLBACK_TIMER
      };
      config.Anonymous1.pszMainIcon = PInvoke.TD_INFORMATION_ICON;

      if (request.OneButton) {
        config.dwCommonButtons = TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON;
      }
      else {
        var buttons = stackalloc TASKDIALOG_BUTTON[2];
        buttons[0] = new TASKDIALOG_BUTTON { nButtonID = OK_BUTTON, pszButtonText = new PCWSTR(okLabel) };
        buttons[1] = new TASKDIALOG_BUTTON { nButtonID = CANCEL_BUTTON, pszButtonText = new PCWSTR(cancelLabel) };
        config.cButtons = 2;
        config.pButtons = buttons;
        config.nDefaultButton = OK_BUTTON;
      }

      var state = new DialogState(request.TimeoutSeconds, request.OneButton ? OK_BUTTON : CANCEL_BUTTON, ct);
      var handle = GCHandle.Alloc(state);
      try {
        config.pfCallback = _callback;
        config.lpCallbackData = GCHandle.ToIntPtr(handle);
        PInvoke.TaskDialogIndirect(in config, out var selected, out var _, out var _).ThrowOnFailure();
        ct.ThrowIfCancellationRequested();
        if (state.TimedOut) {
          throw new TimeoutException("Windows dialog timed out.");
        }

        return request.OneButton || selected == OK_BUTTON;
      }
      finally { handle.Free(); }
    }
  }

  private static HRESULT OnNotification(HWND hwnd, TASKDIALOG_NOTIFICATIONS notification,
    WPARAM timer, LPARAM lParam, nint stateHandle) {
    if (notification != TASKDIALOG_NOTIFICATIONS.TDN_TIMER ||
        GCHandle.FromIntPtr(stateHandle).Target is not DialogState state) {
      return new HRESULT(0);
    }

    if (state.Token.IsCancellationRequested ||
        state.TimeoutMilliseconds > 0 && (long)timer.Value >= state.TimeoutMilliseconds) {
      state.TimedOut = !state.Token.IsCancellationRequested;
      _ = PInvoke.SendMessage(hwnd, (uint)TASKDIALOG_MESSAGES.TDM_CLICK_BUTTON,
        new WPARAM((nuint)state.CloseButton));
    }

    return new HRESULT(0);
  }

  private sealed class DialogState(int timeoutSeconds, int closeButton, CancellationToken token) {
    public CancellationToken Token { get; } = token;
    public long TimeoutMilliseconds { get; } = Math.Max(0, (long)timeoutSeconds * 1000);
    public int CloseButton { get; } = closeButton;
    public bool TimedOut { get; set; }
  }
}
