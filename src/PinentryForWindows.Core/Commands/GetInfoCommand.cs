// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Server;
using PinentryForWindows.Core.Protocol;

namespace PinentryForWindows.Core.Commands;

internal sealed class GetInfoCommand : ICommandHandler<PinentrySessionState> {
  public string Name => "GETINFO";
  public string Description => "Return pinentry metadata";

  public ValueTask HandleAsync(CommandContext<PinentrySessionState> context, CancellationToken ct)
    => HandleCoreAsync(context, ct);

  private static async ValueTask HandleCoreAsync(CommandContext<PinentrySessionState> context, CancellationToken ct) {
    var key = WireText.Require(context.Arguments);
    var value = key switch {
      "version" => context.State.ProductVersion,
      "pid" => Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture),
      "flavor" => "pinentry-for-windows",
      "ttyinfo" => "CONOUT$ w32",
      var _ => throw HandlerException.Remote(67109144, "unknown value")
    };
    await context.SendDataAsync(Encoding.UTF8.GetBytes(value), ct).ConfigureAwait(false);
  }
}
