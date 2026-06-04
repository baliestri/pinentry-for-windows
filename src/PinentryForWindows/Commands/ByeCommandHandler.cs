// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;

namespace PinentryForWindows.Commands;

internal sealed class ByeCommandHandler : CommandHandler {
  /// <inheritdoc />
  public override string Name => "BYE";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    var response = AssuanResponse.Ok("closing connection");
    await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
    serverContext.Session.CloseGracefully();

    Environment.Exit(0); // maybenot
  }
}
