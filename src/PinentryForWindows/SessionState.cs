// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows;

internal static class SessionState {
  static SessionState()
    => Reset();

  public static string Description { get; set; } = string.Empty;
  public static string Prompt { get; set; } = string.Empty;
  public static string Title { get; set; } = string.Empty;
  public static string CachePrefix { get; set; } = string.Empty;
  public static string CacheUser { get; set; } = string.Empty;
  public static string KeyInfo { get; set; } = string.Empty;
  public static string KeyInfoType { get; set; } = string.Empty;
  public static string KeyGrip { get; set; } = string.Empty;
  public static string OkButtonText { get; set; } = string.Empty;
  public static string CancelButtonText { get; set; } = string.Empty;
  public static string NotOkButtonText { get; set; } = string.Empty;
  public static string Error { get; set; } = string.Empty;
  public static int Timeout { get; set; }
  public static bool ExternalPassphraseCache { get; set; }
  public static bool RepeatPassphrase { get; set; }
  public static string RepeatDescription { get; set; } = string.Empty;
  public static string RepeatError { get; set; } = string.Empty;
  public static bool GrabKeyboard { get; set; }

  public static void Reset() {
    Description = "Enter password for GPG key";
    Prompt = "Password:";
    Title = "Pinentry for Windows";
    CachePrefix = "pfwcache:";
    CacheUser = string.Empty;
    KeyInfo = string.Empty;
    KeyInfoType = string.Empty;
    KeyGrip = string.Empty;
    OkButtonText = "OK";
    CancelButtonText = "Cancel";
    NotOkButtonText = "Do not do this";
    Error = string.Empty;
    Timeout = 0;
    ExternalPassphraseCache = false;
    RepeatPassphrase = false;
    RepeatDescription = "Please re-enter your passphrase:";
    RepeatError = "Passphrases do not match. Please try again.";
    GrabKeyboard = false;
  }
}
