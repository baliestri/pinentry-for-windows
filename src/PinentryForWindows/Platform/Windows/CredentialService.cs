// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using PinentryForWindows.Services;

namespace PinentryForWindows.Platform.Windows;

internal sealed class CredentialService : ICredentialService {
  private const string WINDOWS_HELLO_KEY_NAME = "PinentryForWindows.CacheUnlock.v1";

  /// <inheritdoc />
  public async Task<string?> PromptAsync(string title, string message, string userName, CancellationToken ct = default) {
    if (ct.IsCancellationRequested) {
      return null;
    }

    // CredUIPromptForWindowsCredentials doesn't expose a timeout or a safe way to cancel an already displayed dialog.
    return await Task.Run(() => PromptWithNativeCredentialDialog(title, message, userName), ct);
  }

  /// <inheritdoc />
  public async Task<string?> TryGetCachedAsync(string cacheKey, CancellationToken ct = default) {
    var vault = new PasswordVault();
    try {
      var credentials = vault.FindAllByResource(cacheKey);
      if (credentials.Count == 0) {
        return null;
      }

      if (!await UnlockWithWindowsHelloAsync(WINDOWS_HELLO_KEY_NAME, ct)) {
        return null;
      }

      var credential = credentials[0];
      credential.RetrievePassword();

      var stored = credential.Password;
      if (string.IsNullOrWhiteSpace(stored)) {
        return null;
      }

      // Legacy plaintext entry (pre-v2, no pfwv2: prefix): remove it and treat as cache miss.
      if (!stored.StartsWith("pfwv2:", StringComparison.Ordinal)) {
        RemoveFromPasswordVault(vault, cacheKey);
        return null;
      }

      return CacheEntryProtector.TryUnprotect(stored, cacheKey);
    }
    catch {
      // Ignore any errors during retrieval
      return null;
    }
  }

  /// <inheritdoc />
  public Task StoreAsync(string cacheKey, string userName, string password, CancellationToken ct = default) {
    var vault = new PasswordVault();
    RemoveFromPasswordVault(vault, cacheKey);
    vault.Add(new PasswordCredential(cacheKey, userName, CacheEntryProtector.Protect(password, cacheKey)));

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public Task RemoveAsync(string cacheKey, CancellationToken ct = default) {
    var vault = new PasswordVault();
    RemoveFromPasswordVault(vault, cacheKey);

    return Task.CompletedTask;
  }

  private static void RemoveFromPasswordVault(PasswordVault vault, string cacheKey) {
    try {
      foreach (var credential in vault.FindAllByResource(cacheKey)) {
        vault.Remove(credential);
      }
    }
    catch {
      // Ignore any errors during removal
    }
  }

  private static async Task<bool> UnlockWithWindowsHelloAsync(string keyName, CancellationToken ct) {
    try {
      if (!await KeyCredentialManager.IsSupportedAsync().AsTask(ct)) {
        return false;
      }

      var credential = await OpenOrCreateWindowsHelloKeyAsync(keyName, ct);
      if (credential is null) {
        return false;
      }

      var challengeBytes = new byte[32];
      RandomNumberGenerator.Fill(challengeBytes);

      var challenge = CryptographicBuffer.CreateFromByteArray(challengeBytes);
      var signResult = await credential.RequestSignAsync(challenge).AsTask(ct);

      return signResult.Status == KeyCredentialStatus.Success;
    }
    catch {
      return false;
    }
  }

  private static async Task<KeyCredential?> OpenOrCreateWindowsHelloKeyAsync(string keyName, CancellationToken ct) {
    var openResult = await KeyCredentialManager.OpenAsync(keyName).AsTask(ct);
    if (openResult.Status == KeyCredentialStatus.Success) {
      return openResult.Credential;
    }

    if (openResult.Status != KeyCredentialStatus.NotFound) {
      return null;
    }

    var createResult = await KeyCredentialManager.RequestCreateAsync(keyName, KeyCredentialCreationOption.FailIfExists).AsTask(ct);
    return createResult.Status == KeyCredentialStatus.Success ? createResult.Credential : null;
  }

  private static string? PromptWithNativeCredentialDialog(string title, string message, string lockedUserName) {
    var options = new CredentialManager.PromptForWindowsCredentialsOptions(title, message) {
      IsSaveChecked = false,
      Flags = CredentialManager.PromptForWindowsCredentialsFlag.CredUiWinGeneric |
              CredentialManager.PromptForWindowsCredentialsFlag.CredUiWinInCredOnly
    };

    var result = CredentialManager.PromptForWindowsCredentials(options, lockedUserName, string.Empty);
    if (result is null ||
        string.IsNullOrWhiteSpace(result.Password)) {
      return null;
    }

    if (!string.Equals(result.UserName, lockedUserName, StringComparison.OrdinalIgnoreCase)) {
      return null;
    }

    return result.Password;
  }
}
