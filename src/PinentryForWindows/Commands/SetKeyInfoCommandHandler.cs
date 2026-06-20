// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.RegularExpressions;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;

namespace PinentryForWindows.Commands;

internal sealed partial class SetKeyInfoCommandHandler : CommandHandler {
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
        SessionState.KeyInfoType = string.Empty;
        SessionState.KeyGrip = string.Empty;

        var response = AssuanResponse.Ok();
        await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
        return;
      }
      default: {
        var argument = command.Arguments[0];
        SessionState.KeyInfo = argument;

        var match = KeyInfoFormatRegex().Match(argument);
        if (match.Success) {
          SessionState.KeyInfoType = match.Groups[1].Value;
          SessionState.KeyGrip = match.Groups[2].Value.ToUpperInvariant();
        }
        else {
          SessionState.KeyInfoType = string.Empty;
          SessionState.KeyGrip = string.Empty;
        }

        var response = AssuanResponse.Ok();
        await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
        break;
      }
    }
  }

  // Matches the gpg-agent SETKEYINFO format: X/[40-hex-keygrip]
  // X is the key type: n = OpenPGP, s = SSH, u = other/unspecified
  [GeneratedRegex("^([nsu])/([0-9A-Fa-f]{40})$", RegexOptions.Compiled | RegexOptions.Singleline)]
  private static partial Regex KeyInfoFormatRegex();
}
