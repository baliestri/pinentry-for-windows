// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;

namespace PinentryForWindows.Commands;

internal sealed class SetTimeoutCommandHandler : CommandHandler {
  /// <inheritdoc />
  public override string Name => "SETTIMEOUT";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    if (command.Arguments.Length == 1 &&
        int.TryParse(command.Arguments[0], out var timeout)) {
      SessionState.Timeout = timeout;
    }

    var response = AssuanResponse.Ok();
    await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
  }
}
