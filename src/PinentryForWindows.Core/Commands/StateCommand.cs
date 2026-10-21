// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server;
using PinentryForWindows.Core.Protocol;

namespace PinentryForWindows.Core.Commands;

internal sealed class StateCommand(string name) : ICommandHandler<PinentrySessionState> {
  public string Name { get; } = name;
  public string Description => "Set pinentry session metadata";

  public ValueTask HandleAsync(CommandContext<PinentrySessionState> context, CancellationToken ct) {
    var state = context.State;
    var fallbackEncoding = Name is "SETKEYINFO" or "SETTIMEOUT" ? null : state.TextFallbackEncoding;
    var value = Name == "SETTIMEOUT" ? WireText.Decode(context.Arguments) : WireText.Require(context.Arguments, fallbackEncoding);
    switch (Name) {
      case "SETTITLE":
        state.Title = value;
        break;
      case "SETDESC":
        state.SetDescription(value);
        break;
      case "SETPROMPT":
        state.PromptLabel = value;
        break;
      case "SETERROR":
        state.Error = value;
        break;
      case "SETKEYINFO":
        if (value.AsSpan().IndexOfAny(' ', '\t') >= 0) {
          throw HandlerException.Remote(67109144, "invalid key info");
        }

        state.SetKeyInfo(value);
        break;
      case "SETREPEAT":
        state.Repeat = true;
        state.RepeatDescription = value;
        break;
      case "SETREPEATERROR":
        state.RepeatError = value;
        break;
      case "SETOK":
        state.OkLabel = value.Replace('_', '&');
        break;
      case "SETCANCEL":
        state.CancelLabel = value.Replace('_', '&');
        break;
      case "SETNOTOK":
        state.NotOkLabel = value.Replace('_', '&');
        break;
      case "SETTIMEOUT":
        if (int.TryParse(value, out var seconds) &&
            seconds is >= 0 and <= 300) {
          state.TimeoutSeconds = seconds;
        }

        break;
    }

    return ValueTask.CompletedTask;
  }
}
