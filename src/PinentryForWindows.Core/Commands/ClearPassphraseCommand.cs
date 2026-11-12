// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server;
using PinentryForWindows.Core.Protocol;

namespace PinentryForWindows.Core.Commands;

internal sealed class ClearPassphraseCommand : ICommandHandler<PinentrySessionState> {
  public string Name => "CLEARPASSPHRASE";
  public string Description => "Forget a cached passphrase";

  public async ValueTask HandleAsync(CommandContext<PinentrySessionState> context, CancellationToken ct) {
    var value = WireText.Require(context.Arguments);
    if (value.Length is not (40 or 42)) {
      throw HandlerException.Remote(67109144, "invalid key");
    }

    var grip = value.Length == 42 && value[1] == '/' ? value[2..] : value;
    if (grip.Length != 40 ||
        !grip.All(Uri.IsHexDigit)) {
      throw HandlerException.Remote(67109144, "invalid key");
    }

    await context.State.Cache.RemoveAsync(context.State.Origin, grip.ToUpperInvariant(), ct).ConfigureAwait(false);
  }
}
