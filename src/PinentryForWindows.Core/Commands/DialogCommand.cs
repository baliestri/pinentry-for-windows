// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server;
using PinentryForWindows.Core.Contracts;
using PinentryForWindows.Core.Protocol;

namespace PinentryForWindows.Core.Commands;

internal sealed class DialogCommand(string name) : ICommandHandler<PinentrySessionState> {
  public string Name { get; } = name;
  public string Description => "Show a Windows message or confirmation";

  public async ValueTask HandleAsync(CommandContext<PinentrySessionState> context, CancellationToken ct) {
    var state = context.State;
    var raw = context.Arguments;
    var oneButton = string.Equals(Name, "CONFIRM", StringComparison.Ordinal) &&
                    raw.Span.SequenceEqual("--one-button"u8);
    var description = string.Equals(Name, "MESSAGE", StringComparison.Ordinal) && !raw.IsEmpty
      ? WireText.Decode(raw, state.TextFallbackEncoding)
      : state.Description;
    var request = new DialogRequest(state.Title, description, state.OkLabel,
      state.CancelLabel, oneButton || string.Equals(Name, "MESSAGE", StringComparison.Ordinal), state.TimeoutSeconds);
    try {
      if (request.OneButton) {
        await state.Ui.MessageAsync(request, ct).ConfigureAwait(false);
      }
      else if (!await state.Ui.ConfirmAsync(request, ct).ConfigureAwait(false)) {
        throw HandlerException.Remote(83886179, "cancelled");
      }
    }
    catch (TimeoutException) { throw HandlerException.Remote(83886142, "timeout"); }
  }
}
