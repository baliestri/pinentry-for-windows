// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Reflection;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using PinentryForWindows.Extensions;

namespace PinentryForWindows.Commands;

internal sealed class GetInfoCommandHandler : CommandHandler {
  /// <inheritdoc />
  public override string Name => "GETINFO";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    if (command.Arguments.Length is 0 or > 1) {
      var response = AssuanResponse.Error(ExitCode.UNKNOWN_VALUE, "unknown value for WHAT");
      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      return;
    }

    switch (command.Arguments[0]) {
      case "version": {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        var responseCollection = AssuanResponseCollection.Create(AssuanResponse.Data(version), AssuanResponse.Ok());
        await serverContext.SendResponseAsync(responseCollection, serverContext.Session.CancellationToken);
        break;
      }
      case "pid": {
        var responseCollection = AssuanResponseCollection.Create(AssuanResponse.Data(Environment.ProcessId.ToString()), AssuanResponse.Ok());
        await serverContext.SendResponseAsync(responseCollection, serverContext.Session.CancellationToken);
        break;
      }
      case "flavor": {
        var responseCollection = AssuanResponseCollection.Create(AssuanResponse.Data("pinentry-for-windows"), AssuanResponse.Ok());
        await serverContext.SendResponseAsync(responseCollection, serverContext.Session.CancellationToken);
        break;
      }
      case "ttyinfo": {
        const string TTY_INFO = "CONOUT$ w32";

        var responseCollection = AssuanResponseCollection.Create(AssuanResponse.Data(TTY_INFO), AssuanResponse.Ok());
        await serverContext.SendResponseAsync(responseCollection, serverContext.Session.CancellationToken);
        break;
      }
      default: {
        var response = AssuanResponse.Error(ExitCode.UNKNOWN_VALUE, $"unknown value for '{command.AsText()}'");
        await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
        break;
      }
    }
  }
}
