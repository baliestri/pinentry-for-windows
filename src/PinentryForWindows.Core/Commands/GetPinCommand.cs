// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Security.Cryptography;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;
using PinentryForWindows.Core.Contracts;
using PinentryForWindows.Core.Protocol;

namespace PinentryForWindows.Core.Commands;

internal sealed class GetPinCommand : ICommandHandler<PinentrySessionState> {
  private const string CREDENTIAL_TITLE = "Pinentry for Windows";
  public string Name => "GETPIN";
  public string Description => "Ask for a passphrase";

  public async ValueTask HandleAsync(CommandContext<PinentrySessionState> context, CancellationToken ct) {
    var receivedAt = Stopwatch.GetTimestamp();
    var state = context.State;
    var error = state.Error;
    state.Error = string.Empty;
    var rejectedGrip = state.LastCachedGrip;
    state.LastCachedGrip = null;

    if (error.Length == 0 &&
        await TryAnswerFromCacheAsync(context, ct).ConfigureAwait(false)) {
      return;
    }

    if (error.Length != 0 &&
        rejectedGrip is not null &&
        string.Equals(rejectedGrip, state.KeyGrip, StringComparison.Ordinal)) {
      // The agent rejected the passphrase this session just served from the cache, so the entry is stale.
      // An error after typed input proves nothing about the entry and must leave it alone.
      try { await state.Cache.RemoveAsync(state.Origin, rejectedGrip, ct).ConfigureAwait(false); }
      catch (Exception) when (!ct.IsCancellationRequested) {
        /* A stale entry that cannot be removed is still skipped for this request. */
      }
    }

    var description = error.Length == 0 ? state.Description : error + Environment.NewLine + state.Description;
    var request = new PromptRequest(CREDENTIAL_TITLE, description, state.UserLabel,
      state.PromptLabel, state.CacheEligible, state.TimeoutSeconds, receivedAt);
    using var first = await PromptAsync(state.Ui, request, ct).ConfigureAwait(false);

    if (first is null) {
      throw HandlerException.Remote(83886179, "cancelled");
    }

    if (state.Repeat) {
      await ConfirmRepeatAsync(context, request, first, ct).ConfigureAwait(false);
    }

    await SendSecretAsync(context, first.Secret, ct).ConfigureAwait(false);

    if (state.CacheEligible &&
        first.Remember) {
      var copy = first.Secret.Expose().ToArray();
      try { await state.Cache.WriteAsync(state.Origin, state.KeyGrip!, copy, ct).ConfigureAwait(false); }
      catch (Exception) when (!ct.IsCancellationRequested) {
        /* A successful input does not require caching. */
      }
      finally { CryptographicOperations.ZeroMemory(copy); }
    }
  }

  private static async ValueTask<bool> TryAnswerFromCacheAsync(CommandContext<PinentrySessionState> context,
    CancellationToken ct) {
    var state = context.State;
    if (!state.CacheEligible) {
      return false;
    }

    SecretBuffer? cached = null;
    try { cached = await state.Cache.ReadAsync(state.Origin, state.KeyGrip!, ct).ConfigureAwait(false); }
    catch (Exception) when (!ct.IsCancellationRequested) {
      /* Cache failure falls back to input. */
    }

    if (cached is null) {
      return false;
    }

    using (cached) {
      await context.SendStatusAsync("PASSWORD_FROM_CACHE", ReadOnlyMemory<byte>.Empty, ct).ConfigureAwait(false);
      await SendSecretAsync(context, cached, ct).ConfigureAwait(false);
    }

    state.LastCachedGrip = state.KeyGrip;
    return true;
  }

  private static async ValueTask ConfirmRepeatAsync(CommandContext<PinentrySessionState> context,
    PromptRequest request, PasswordPromptResult first, CancellationToken ct) {
    var state = context.State;
    var repeatRequest = request with {
      Description = state.RepeatDescription, OfferSave = false,
      GetPinTimestamp = Stopwatch.GetTimestamp()
    };

    using var second = await PromptAsync(state.Ui, repeatRequest, ct).ConfigureAwait(false);

    if (second is null) {
      throw HandlerException.Remote(83886179, "cancelled");
    }

    if (!CryptographicOperations.FixedTimeEquals(first.Secret.Expose(), second.Secret.Expose())) {
      throw HandlerException.Remote(83886194, "PIN not repeated");
    }

    await context.SendStatusAsync("PIN_REPEATED", ReadOnlyMemory<byte>.Empty, ct).ConfigureAwait(false);
  }

  private static async ValueTask SendSecretAsync(CommandContext<PinentrySessionState> context, SecretBuffer secret,
    CancellationToken ct) {
    var bytes = secret.Expose().ToArray();
    try { await context.SendDataAsync(bytes, ct).ConfigureAwait(false); }
    finally { CryptographicOperations.ZeroMemory(bytes); }
  }

  private static async ValueTask<PasswordPromptResult?> PromptAsync(IPinentryUi ui, PromptRequest request,
    CancellationToken ct) {
    try { return await ui.PromptAsync(request, ct).ConfigureAwait(false); }
    catch (TimeoutException) { throw HandlerException.Remote(83886142, "timeout"); }
    catch (SecretTooLongException) { throw HandlerException.Remote(83886147, "passphrase too long"); }
  }
}
