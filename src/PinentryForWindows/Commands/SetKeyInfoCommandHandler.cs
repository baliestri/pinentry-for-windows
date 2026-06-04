// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;

namespace PinentryForWindows.Commands;

internal sealed class SetKeyInfoCommandHandler : CommandHandler {
  /// <inheritdoc />
  public override string Name => "SETKEYINFO";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    if (command.Arguments.Length is 0 or > 1) {
      var response = AssuanResponse.Error(ExitCode.UNKNOWN_VALUE, "unknown value for WHAT");
      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      return;
    }

    switch (command.Arguments[0]) {
      case "--clear": {
        SessionState.KeyInfo = string.Empty;

        var response = AssuanResponse.Ok();
        await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
        return;
      }
      default: {
        SessionState.KeyInfo = command.Arguments[0];

        var response = AssuanResponse.Ok();
        await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
        break;
      }
    }
  }
}
