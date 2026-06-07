// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using PinentryForWindows.Extensions;
using PinentryForWindows.Platform.Windows;
using PinentryForWindows.Services;

namespace PinentryForWindows.Commands;

internal sealed class MessageCommandHandler(IDialogService dialogService) : CommandHandler {
  public MessageCommandHandler() : this(new DialogService()) { }

  /// <inheritdoc />
  public override string Name => "MESSAGE";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    var description = command.Arguments.Length > 0 ? command.AsText() : SessionState.Description;

    var dialogResponse = dialogService.ShowMessage(SessionState.Title, description, SessionState.Timeout);

    var response = dialogResponse == DialogResponse.TimedOut
      ? AssuanResponse.Error(ExitCode.TIMEOUT, "timeout")
      : AssuanResponse.Ok();

    await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
  }
}
