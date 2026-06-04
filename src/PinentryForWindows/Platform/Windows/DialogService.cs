// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Services;

namespace PinentryForWindows.Platform.Windows;

internal sealed class DialogService : IDialogService {
  private static int _visualStylesEnabled;

  /// <inheritdoc />
  public void ShowMessage(string title, string message)
    => _ = Show(title, message, TaskDialogIcon.Information, [TaskDialogButton.OK]);

  /// <inheritdoc />
  public void ShowWarning(string title, string message)
    => _ = Show(title, message, TaskDialogIcon.Warning, [TaskDialogButton.OK]);

  /// <inheritdoc />
  public bool Confirm(string title, string message, string? okButtonText = null, string? cancelButtonText = null) {
    var okButton = !string.IsNullOrWhiteSpace(okButtonText) ? new TaskDialogButton(okButtonText) : TaskDialogButton.OK;
    var cancelButton = !string.IsNullOrWhiteSpace(cancelButtonText) ? new TaskDialogButton(cancelButtonText) : TaskDialogButton.Cancel;

    return Show(title, message, TaskDialogIcon.Information, [okButton, cancelButton]) == okButton;
  }

  private static TaskDialogButton Show(string title, string message, TaskDialogIcon icon, IReadOnlyList<TaskDialogButton> buttons) {
    EnsureVisualStylesEnabled();

    var page = new TaskDialogPage {
      Caption = title,
      Text = message,
      Icon = icon,
      AllowCancel = buttons.Contains(TaskDialogButton.Cancel),
      AllowMinimize = false,
      Buttons = [..buttons]
    };

    return TaskDialog.ShowDialog(page);
  }

  private static void EnsureVisualStylesEnabled() {
    if (Interlocked.CompareExchange(ref _visualStylesEnabled, 1, 0) == 0) {
      Application.EnableVisualStyles();
    }
  }
}
