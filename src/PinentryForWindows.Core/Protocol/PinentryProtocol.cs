// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Server;
using AssuanLibrary.Transport;
using PinentryForWindows.Core.Commands;
using PinentryForWindows.Core.Contracts;

namespace PinentryForWindows.Core.Protocol;

public static class PinentryProtocol {
  private static readonly CommandRegistry<PinentrySessionState> _registry = CreateRegistry();
  private static readonly PinentryHooks _hooks = new();
  private static readonly ServerOptions _options = new(commandTimeout: TimeSpan.FromMinutes(5));

  public static async Task RunAsync(Stream stream, string origin, bool authenticated, IPinentryUi ui,
  IPinentryCache cache, CancellationToken ct, string? productVersion = null, Encoding? textFallbackEncoding = null) {
    ArgumentNullException.ThrowIfNull(stream);
    ArgumentException.ThrowIfNullOrWhiteSpace(origin);
    var state = new PinentrySessionState(origin, authenticated, ui, cache,
      productVersion ?? typeof(PinentryProtocol).Assembly.GetName().Version?.ToString() ?? "unknown", textFallbackEncoding);
    state.ResetTransient();
    await using var session = new ServerSession<PinentrySessionState>(
      new AcceptedConnection(stream), state, _registry, _hooks, _options);
    await session.RunAsync(ct);
  }

  private static CommandRegistry<PinentrySessionState> CreateRegistry() {
    var registry = new CommandRegistry<PinentrySessionState>();
    foreach (var name in new[] {
               "SETTITLE", "SETDESC", "SETPROMPT", "SETERROR", "SETKEYINFO", "SETREPEAT",
               "SETREPEATERROR", "SETOK", "SETCANCEL", "SETNOTOK", "SETTIMEOUT"
             }) {
      registry.Register(new StateCommand(name));
    }

    registry.Register(new GetPinCommand());
    registry.Register(new GetInfoCommand());
    registry.Register(new ClearPassphraseCommand());
    registry.Register(new DialogCommand("CONFIRM"));
    registry.Register(new DialogCommand("MESSAGE"));

    return registry;
  }

  private sealed class PinentryHooks : ISessionHooks<PinentrySessionState> {
    public ValueTask AuthenticateAsync(CommandContext<PinentrySessionState> context, CancellationToken ct)
      => context.State.Authenticated
        ? ValueTask.CompletedTask
        : ValueTask.FromException(HandlerException.Remote(55, "authentication required"));

    public ValueTask OptionAsync(OptionRequest request, CommandContext<PinentrySessionState> context,
    CancellationToken ct) {
      var name = Encoding.ASCII.GetString(request.Name.Span);
      var raw = request.Value ?? ReadOnlyMemory<byte>.Empty;
      switch (name) {
        case "allow-external-password-cache":
          context.State.AllowExternalCache = true;
          break;
        case "default-ok":
          context.State.OkLabel = WireText.Decode(raw, context.State.TextFallbackEncoding).Replace('_', '&');
          break;
        case "default-cancel":
          context.State.CancelLabel = WireText.Decode(raw, context.State.TextFallbackEncoding).Replace('_', '&');
          break;
        case "default-notok":
          context.State.NotOkLabel = WireText.Decode(raw, context.State.TextFallbackEncoding).Replace('_', '&');
          break;
        case "default-prompt":
          context.State.PromptLabel = WireText.Decode(raw, context.State.TextFallbackEncoding);
          break;
      }

      return ValueTask.CompletedTask;
    }

    public ValueTask ResetAsync(CommandContext<PinentrySessionState> context, CancellationToken ct) {
      context.State.ResetTransient();
      return ValueTask.CompletedTask;
    }

    public ValueTask ClosedAsync(PinentrySessionState state, SessionEnd reason, CancellationToken ct)
      => ValueTask.CompletedTask;
  }
}
