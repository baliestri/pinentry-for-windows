// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using PinentryForWindows.Platform.Windows;
using PinentryForWindows.Services;

namespace PinentryForWindows.Commands;

internal sealed class ConfirmCommandHandler(IDialogService dialogService) : CommandHandler {
  public ConfirmCommandHandler() : this(new DialogService()) { }

  /// <inheritdoc />
  public override string Name => "CONFIRM";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    if (command.Arguments.Length == 1 &&
        command.Arguments[0].Equals("--one-button", StringComparison.OrdinalIgnoreCase)) {
      var dialogResponse = dialogService.ShowMessage(SessionState.Title, SessionState.Description, SessionState.Timeout);

      var response = dialogResponse == DialogResponse.TimedOut
        ? AssuanResponse.Error(ExitCode.TIMEOUT, "timeout")
        : AssuanResponse.Ok();

      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      return;
    }

    var confirmResponse = dialogService.Confirm(SessionState.Title, SessionState.Description, SessionState.OkButtonText,
      SessionState.CancelButtonText, SessionState.Timeout);

    var finalResponse = confirmResponse switch {
      DialogResponse.Accepted => AssuanResponse.Ok(),
      DialogResponse.TimedOut => AssuanResponse.Error(ExitCode.TIMEOUT, "timeout"),
      var _ => AssuanResponse.Error(ExitCode.CANCELLED, "cancelled")
    };

    await serverContext.SendResponseAsync(finalResponse, serverContext.Session.CancellationToken);
  }
}
