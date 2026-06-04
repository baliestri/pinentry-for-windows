// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using PinentryForWindows.Platform.Windows;
using PinentryForWindows.Services;

namespace PinentryForWindows.Commands;

internal sealed class ClearPassphraseCommandHandler(ICredentialService credentialService) : CommandHandler {
  public ClearPassphraseCommandHandler() : this(new CredentialService()) { }

  /// <inheritdoc />
  public override string Name => "CLEARPASSPHRASE";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    if (command.Arguments.Length is 0 or > 1) {
      var response = AssuanResponse.Error(ExitCode.UNKNOW_VALUE, "unknown value for WHAT");
      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      return;
    }

    try {
      var key = SessionState.CachePrefix + command.Arguments[0];
      await credentialService.RemoveAsync(key);
      var defaultResponse = AssuanResponse.Ok();
      await serverContext.SendResponseAsync(defaultResponse, serverContext.Session.CancellationToken);
    }
    catch {
      var response = AssuanResponse.Error(1, "failed to clear passphrase from cache");
      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
    }
  }
}
