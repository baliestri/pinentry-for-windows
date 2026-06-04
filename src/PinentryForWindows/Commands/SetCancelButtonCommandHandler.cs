// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using PinentryForWindows.Extensions;

namespace PinentryForWindows.Commands;

internal sealed class SetCancelButtonCommandHandler : CommandHandler {
  /// <inheritdoc />
  public override string Name => "SETCANCEL";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    if (command.Arguments.Length is 0) {
      var response = AssuanResponse.Error(ExitCode.UNKNOW_VALUE, "unknown value for WHAT");
      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      return;
    }

    SessionState.CancelButtonText = command.AsText().Replace('_', '&');

    var defaultResponse = AssuanResponse.Ok();
    await serverContext.SendResponseAsync(defaultResponse, serverContext.Session.CancellationToken);
  }
}
