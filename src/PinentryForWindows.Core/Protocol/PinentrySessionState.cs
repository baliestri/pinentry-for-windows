// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using System.Text.RegularExpressions;
using PinentryForWindows.Core.Contracts;

namespace PinentryForWindows.Core.Protocol;

public sealed partial class PinentrySessionState(string origin,
  bool authenticated,
  IPinentryUi ui,
  IPinentryCache cache,
  string productVersion,
  Encoding? textFallbackEncoding = null) {
  private bool _cardDescription;
  private bool _keyInfoCleared;
  public string Origin { get; } = origin;
  public bool Authenticated { get; } = authenticated;
  public IPinentryUi Ui { get; } = ui;
  public IPinentryCache Cache { get; } = cache;
  public string ProductVersion { get; } = productVersion;
  public Encoding? TextFallbackEncoding { get; } = textFallbackEncoding;

  public string Title { get; set; } = string.Empty;
  public string Description { get; set; } = string.Empty;
  public string PromptLabel { get; set; } = string.Empty;
  public string Error { get; set; } = string.Empty;
  public string OkLabel { get; set; } = string.Empty;
  public string CancelLabel { get; set; } = string.Empty;
  public string NotOkLabel { get; set; } = string.Empty;
  public string RepeatDescription { get; set; } = string.Empty;
  public string RepeatError { get; set; } = string.Empty;
  public string CacheUser { get; set; } = string.Empty;
  public string? KeyGrip { get; private set; }
  public bool CardOrToken => _cardDescription || _keyInfoCleared;
  public bool AllowExternalCache { get; set; }
  public bool Repeat { get; set; }
  public int TimeoutSeconds { get; set; }

  public string UserLabel => !string.IsNullOrWhiteSpace(CacheUser) ? CacheUser : KeyGrip ?? Title;

  public bool CacheEligible
    => Cache.IsAvailable && AllowExternalCache && !CardOrToken && KeyGrip is not null && !Repeat;

  public void SetKeyInfo(string value) {
    KeyGrip = null;
    _keyInfoCleared = value == "--clear";
    if (_keyInfoCleared) {
      return;
    }

    var match = KeyGripRegex().Match(value);
    if (match.Success) {
      KeyGrip = match.Groups[1].Value.ToUpperInvariant();
    }
  }

  public void SetDescription(string value) {
    Description = value;
    _cardDescription = value.Contains("smart card", StringComparison.OrdinalIgnoreCase) ||
                       value.Contains("smartcard", StringComparison.OrdinalIgnoreCase) ||
                       value.Contains("yubikey", StringComparison.OrdinalIgnoreCase) ||
                       value.Contains("nitrokey", StringComparison.OrdinalIgnoreCase) ||
                       value.Contains("Holder:", StringComparison.OrdinalIgnoreCase) ||
                       value.Contains("Number:", StringComparison.OrdinalIgnoreCase);

    var match = KeyIdRegex().Match(value);
    CacheUser = match.Success ? match.Groups[1].Value : string.Empty;
  }

  public void ResetTransient() {
    Title = "Pinentry for Windows";
    Description = "Enter password for GPG key";
    PromptLabel = "Password:";
    Error = string.Empty;
    OkLabel = "OK";
    CancelLabel = "Cancel";
    NotOkLabel = "Do not do this";
    RepeatDescription = "Please re-enter your passphrase:";
    RepeatError = "Passphrases do not match. Please try again.";
    CacheUser = string.Empty;
    KeyGrip = null;
    _cardDescription = false;
    _keyInfoCleared = false;
    AllowExternalCache = false;
    Repeat = false;
    TimeoutSeconds = 0;
  }

  [GeneratedRegex("^(?:[nsu]/)?([0-9a-fA-F]{40})$", RegexOptions.CultureInvariant)]
  private static partial Regex KeyGripRegex();

  [GeneratedRegex(@"\bID ([0-9a-fA-F]{16})\b", RegexOptions.CultureInvariant)]
  private static partial Regex KeyIdRegex();
}
