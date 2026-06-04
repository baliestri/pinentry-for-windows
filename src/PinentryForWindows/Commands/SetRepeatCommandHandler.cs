// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using PinentryForWindows.Extensions;

namespace PinentryForWindows.Commands;

internal sealed class SetRepeatCommandHandler : CommandHandler {
  /// <inheritdoc />
  public override string Name => "SETREPEAT";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    if (command.Arguments.Length is 0) {
      var response = AssuanResponse.Error(ExitCode.UNKNOWN_VALUE, "unknown value for WHAT");
      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      return;
    }

    SessionState.RepeatPassphrase = true;
    SessionState.RepeatDescription = command.AsText();

    var defaultResponse = AssuanResponse.Ok();
    await serverContext.SendResponseAsync(defaultResponse, serverContext.Session.CancellationToken);
  }
}
