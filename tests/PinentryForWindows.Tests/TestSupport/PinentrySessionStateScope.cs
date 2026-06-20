// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Configuration;
using PinentryForWindows.Runtime;

namespace PinentryForWindows.Tests.TestSupport;

internal sealed class SessionStateScope : IDisposable {
  private readonly Snapshot _snapshot;

  private SessionStateScope()
    => _snapshot = Snapshot.Capture();

  public static SessionStateScope Create() {
    var scope = new SessionStateScope();
    Reset();
    return scope;
  }

  public void Dispose()
    => _snapshot.Apply();

  public static void Reset() {
    SessionState.Reset();
    InteractivePromptCoordinator.Clear();
    AppConfiguration.AllowUserCacheOptIn = false;
  }

  private sealed record Snapshot(
    string Description,
    string Prompt,
    string Title,
    string CachePrefix,
    string CacheUser,
    string KeyInfo,
    string KeyInfoType,
    string KeyGrip,
    string OkButtonText,
    string CancelButtonText,
    string NotOkButtonText,
    string Error,
    int Timeout,
    bool ExternalPassphraseCache,
    bool RepeatPassphrase,
    string RepeatDescription,
    string RepeatError,
    bool GrabKeyboard,
    bool AllowUserCacheOptIn,
    InteractiveCommandSignal? PromptSignal) {
    public static Snapshot Capture()
      => new(
        SessionState.Description,
        SessionState.Prompt,
        SessionState.Title,
        SessionState.CachePrefix,
        SessionState.CacheUser,
        SessionState.KeyInfo,
        SessionState.KeyInfoType,
        SessionState.KeyGrip,
        SessionState.OkButtonText,
        SessionState.CancelButtonText,
        SessionState.NotOkButtonText,
        SessionState.Error,
        SessionState.Timeout,
        SessionState.ExternalPassphraseCache,
        SessionState.RepeatPassphrase,
        SessionState.RepeatDescription,
        SessionState.RepeatError,
        SessionState.GrabKeyboard,
        AppConfiguration.AllowUserCacheOptIn,
        InteractivePromptCoordinator.Current);

    public void Apply() {
      SessionState.Description = Description;
      SessionState.Prompt = Prompt;
      SessionState.Title = Title;
      SessionState.CachePrefix = CachePrefix;
      SessionState.CacheUser = CacheUser;
      SessionState.KeyInfo = KeyInfo;
      SessionState.KeyInfoType = KeyInfoType;
      SessionState.KeyGrip = KeyGrip;
      SessionState.OkButtonText = OkButtonText;
      SessionState.CancelButtonText = CancelButtonText;
      SessionState.NotOkButtonText = NotOkButtonText;
      SessionState.Error = Error;
      SessionState.Timeout = Timeout;
      SessionState.ExternalPassphraseCache = ExternalPassphraseCache;
      SessionState.RepeatPassphrase = RepeatPassphrase;
      SessionState.RepeatDescription = RepeatDescription;
      SessionState.RepeatError = RepeatError;
      SessionState.GrabKeyboard = GrabKeyboard;
      AppConfiguration.AllowUserCacheOptIn = AllowUserCacheOptIn;
      InteractivePromptCoordinator.Restore(PromptSignal);
    }
  }
}
