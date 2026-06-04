// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;

namespace PinentryForWindows.Commands;

internal sealed class ResetCommandHandler : CommandHandler {
  /// <inheritdoc />
  public override string Name => "RESET";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    SessionState.Reset();

    var response = AssuanResponse.Ok();
    await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
  }
}
