// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using PinentryForWindows.Extensions;

namespace PinentryForWindows.Commands;

internal sealed class OptionCommandHandler : CommandHandler {
  /// <inheritdoc />
  public override string Name => "OPTION";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    if (command.Arguments.Length is 0) {
      var response = AssuanResponse.Error(ExitCode.UNKNOW_VALUE, "unknown value for WHAT");
      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      return;
    }

    var optionParts = command.AsText().Split('=', 2);

    var key = optionParts[0];
    var value = optionParts.Length == 2 ? optionParts[1] : string.Empty;

    switch (key) {
      case "allow-external-password-cache": {
        SessionState.ExternalPassphraseCache = true;
        break;
      }
      case "default-ok": {
        SessionState.OkButtonText = value.Replace('_', '&');
        break;
      }
      case "default-cancel": {
        SessionState.CancelButtonText = value.Replace('_', '&');
        break;
      }
      case "default-notok": {
        SessionState.NotOkButtonText = value.Replace('_', '&');
        break;
      }
      case "default-prompt": {
        SessionState.Prompt = value;
        break;
      }
      case "grab": {
        SessionState.GrabKeyboard = true;
        break;
      }
      case "no-grab": {
        SessionState.GrabKeyboard = false;
        break;
      }
    }

    var defaultResponse = AssuanResponse.Ok();
    await serverContext.SendResponseAsync(defaultResponse, serverContext.Session.CancellationToken);
  }
}
