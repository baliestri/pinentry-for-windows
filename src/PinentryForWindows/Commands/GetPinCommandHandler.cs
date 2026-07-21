// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using PinentryForWindows.Configuration;
using PinentryForWindows.Platform.Windows;
using PinentryForWindows.Runtime;
using PinentryForWindows.Services;

namespace PinentryForWindows.Commands;

internal sealed class GetPinCommandHandler(ICredentialService credentialService, IDialogService dialogService) : CommandHandler {
  public GetPinCommandHandler() : this(new CredentialService(), new DialogService()) { }

  /// <inheritdoc />
  public override string Name => "GETPIN";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    var credentialUser = !string.IsNullOrWhiteSpace(SessionState.CacheUser)
      ? SessionState.CacheUser
      : !string.IsNullOrWhiteSpace(SessionState.KeyInfo)
        ? SessionState.KeyInfo
        : SessionState.Title;

    var cacheKey = !string.IsNullOrWhiteSpace(SessionState.KeyInfo)
      ? SessionState.CachePrefix + SessionState.KeyInfo
      : string.Empty;

    if (string.IsNullOrWhiteSpace(SessionState.Error) &&
        !SessionState.RepeatPassphrase &&
        SessionState.ExternalPassphraseCache &&
        !string.IsNullOrWhiteSpace(cacheKey)) {
      try {
        var cachedPassword = await credentialService.TryGetCachedAsync(cacheKey);
        if (!string.IsNullOrEmpty(cachedPassword)) {
          var responseCollection = AssuanResponseCollection.Create(
            AssuanResponse.Status("PASSWORD_FROM_CACHE"),
            AssuanResponse.Data(cachedPassword),
            AssuanResponse.Ok());

          await serverContext.SendResponseAsync(responseCollection, serverContext.Session.CancellationToken);
          return;
        }
      }
      catch {
        // Best-effort cache lookup; fall back to prompting.
      }
    }

    var pinError = SessionState.Error;
    SessionState.Error = string.Empty;

    var promptMessage = BuildPromptMessage(SessionState.Description, pinError);

    var showSaveCheckbox = AppConfiguration.AllowUserCacheOptIn &&
                           !SessionState.RepeatPassphrase &&
                           !string.IsNullOrWhiteSpace(cacheKey);

    var promptSignal = InteractivePromptCoordinator.Begin();

    try {
      try {
        string? password;
        var userOptedIn = false;

        if (showSaveCheckbox) {
          var result = await credentialService.PromptWithSaveCheckboxAsync(
            SessionState.Title,
            promptMessage,
            credentialUser,
            serverContext.Session.CancellationToken);

          if (result is null) {
            var response = AssuanResponse.Error(ExitCode.CANCELLED, "cancelled");
            await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
            return;
          }

          password = result.Password;
          userOptedIn = result.SaveChecked;
        }
        else {
          password = await credentialService.PromptAsync(
            SessionState.Title,
            promptMessage,
            credentialUser,
            serverContext.Session.CancellationToken);

          if (password is null) {
            var response = AssuanResponse.Error(ExitCode.CANCELLED, "cancelled");
            await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
            return;
          }
        }

        if (SessionState.RepeatPassphrase) {
          var passwordRepeat = await credentialService.PromptAsync(
            SessionState.Title + " - Repeat",
            SessionState.RepeatDescription,
            credentialUser,
            serverContext.Session.CancellationToken);

          if (passwordRepeat is null) {
            var response = AssuanResponse.Error(ExitCode.CANCELLED, "cancelled");
            await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
            return;
          }

          if (!string.Equals(password, passwordRepeat, StringComparison.Ordinal)) {
            dialogService.ShowWarning(SessionState.Title, SessionState.RepeatError);

            var response = AssuanResponse.Error(ExitCode.NOT_CONFIRMED, "PIN not repeated");
            await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
            return;
          }

          var repeatedResponseCollection = AssuanResponseCollection.Create(
            AssuanResponse.Status("PIN_REPEATED"),
            AssuanResponse.Data(password),
            AssuanResponse.Ok());

          await serverContext.SendResponseAsync(repeatedResponseCollection, serverContext.Session.CancellationToken);
        }
        else {
          var responseCollection = AssuanResponseCollection.Create(AssuanResponse.Data(password), AssuanResponse.Ok());
          await serverContext.SendResponseAsync(responseCollection, serverContext.Session.CancellationToken);
        }

        if ((SessionState.ExternalPassphraseCache || userOptedIn) &&
            !string.IsNullOrWhiteSpace(cacheKey)) {
          try {
            await credentialService.StoreAsync(cacheKey, credentialUser, password);
          }
          catch {
            // Best-effort; do not fail GETPIN if caching fails.
          }
        }
      }
      catch (OperationCanceledException) {
        var response = AssuanResponse.Error(ExitCode.CANCELLED, "cancelled");
        await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      }
      catch (Exception) {
        var response = AssuanResponse.Error(ExitCode.CANCELLED, "cancelled");
        await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      }
    }
    finally {
      InteractivePromptCoordinator.Complete(promptSignal);
    }
  }

  private static string BuildPromptMessage(string description, string pinError) {
    var sb = new StringBuilder();
    if (!string.IsNullOrWhiteSpace(pinError)) {
      sb.AppendLine(pinError);
    }

    if (!string.IsNullOrWhiteSpace(description)) {
      sb.Append(description);
    }

    return sb.ToString();
  }
}
