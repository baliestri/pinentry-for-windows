// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.Security.Credentials;

namespace PinentryForWindows.Platform.Windows;

internal static class CredentialManager {
  [Flags]
  public enum PromptForWindowsCredentialsFlag {
    CredUiWinGeneric = 0x00000001,
    CredUiWinCheckbox = 0x00000004,
    CredUiWinInCredOnly = 0x00000020
  }

  private const int CRED_UI_CANCELLED = 1223;

  public static unsafe PromptCredentialsResult? PromptForWindowsCredentials(PromptForWindowsCredentialsOptions options, string userName,
  string password) {
    var authPackage = 0u;
    var save = new BOOL(options.IsSaveChecked);
    var inAuthBuffer = IntPtr.Zero;
    var inAuthBufferSize = 0u;
    void* outAuthBuffer = null;
    var outAuthBufferSize = 0u;

    try {
      if (!string.IsNullOrEmpty(userName) ||
          !string.IsNullOrEmpty(password)) {
        inAuthBufferSize = GetPackedCredentialSize(userName, password);
        inAuthBuffer = Marshal.AllocCoTaskMem((int)inAuthBufferSize);

        fixed (char* userNamePtr = userName)
        fixed (char* passwordPtr = password) {
          var inAuthBufferSpan = new Span<byte>(inAuthBuffer.ToPointer(), checked((int)inAuthBufferSize));
          if (!PInvoke.CredPackAuthenticationBuffer(
                CRED_PACK_FLAGS.CRED_PACK_GENERIC_CREDENTIALS,
                new PWSTR(userNamePtr),
                new PWSTR(passwordPtr),
                inAuthBufferSpan,
                ref inAuthBufferSize)) {
            throw new Win32Exception(Marshal.GetLastWin32Error());
          }
        }
      }

      fixed (char* captionPtr = options.Caption)
      fixed (char* messagePtr = options.Message) {
        var uiInfo = new CREDUI_INFOW {
          cbSize = (uint)Marshal.SizeOf<CREDUI_INFOW>(),
          hwndParent = new HWND(options.HwndParent),
          pszCaptionText = new PCWSTR(captionPtr),
          pszMessageText = new PCWSTR(messagePtr),
          hbmBanner = new HBITMAP(options.HbmBanner)
        };

        var result = PInvoke.CredUIPromptForWindowsCredentials(
          &uiInfo,
          (uint)options.AuthErrorCode,
          &authPackage,
          inAuthBuffer.ToPointer(),
          inAuthBufferSize,
          &outAuthBuffer,
          &outAuthBufferSize,
          &save,
          (CREDUIWIN_FLAGS)options.Flags);

        if (result == 0) {
          return UnpackCredentials(outAuthBuffer, outAuthBufferSize, save.Value != 0);
        }

        if (result == CRED_UI_CANCELLED) {
          return null;
        }

        throw new Win32Exception((int)result);
      }
    }
    finally {
      ZeroFreeCoTaskMem(inAuthBuffer, inAuthBufferSize);
      ZeroFreeCoTaskMem((IntPtr)outAuthBuffer, outAuthBufferSize);
    }
  }

  private static unsafe uint GetPackedCredentialSize(string userName, string password) {
    var size = 0u;

    fixed (char* userNamePtr = userName)
    fixed (char* passwordPtr = password) {
      _ = PInvoke.CredPackAuthenticationBuffer(
        CRED_PACK_FLAGS.CRED_PACK_GENERIC_CREDENTIALS,
        new PWSTR(userNamePtr),
        new PWSTR(passwordPtr),
        [],
        ref size);
    }

    return size > 0 ? size : throw new Win32Exception(Marshal.GetLastWin32Error());
  }

  private static unsafe PromptCredentialsResult UnpackCredentials(void* authBuffer, uint authBufferSize, bool save) {
    var userNameLength = 0u;
    var domainNameLength = 0u;
    var passwordLength = 0u;

    _ = PInvoke.CredUnPackAuthenticationBuffer(
      CRED_PACK_FLAGS.CRED_PACK_GENERIC_CREDENTIALS,
      authBuffer,
      authBufferSize,
      new PWSTR(null),
      &userNameLength,
      new PWSTR(null),
      &domainNameLength,
      new PWSTR(null),
      &passwordLength);

    if (userNameLength <= 0 ||
        passwordLength <= 0) {
      throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    var userName = new char[userNameLength];
    var domainName = new char[domainNameLength];
    var password = new char[passwordLength];

    try {
      fixed (char* userNamePtr = userName)
      fixed (char* domainNamePtr = domainName)
      fixed (char* passwordPtr = password) {
        if (!PInvoke.CredUnPackAuthenticationBuffer(
              CRED_PACK_FLAGS.CRED_PACK_GENERIC_CREDENTIALS,
              authBuffer,
              authBufferSize,
              new PWSTR(userNamePtr),
              &userNameLength,
              new PWSTR(domainNamePtr),
              &domainNameLength,
              new PWSTR(passwordPtr),
              &passwordLength)) {
          throw new Win32Exception(Marshal.GetLastWin32Error());
        }
      }

      return new PromptCredentialsResult {
        UserName = ToStringFromBuffer(userName),
        DomainName = ToStringFromBuffer(domainName),
        Password = ToStringFromBuffer(password),
        IsSaveChecked = save
      };
    }
    finally {
      Array.Clear(password, 0, password.Length);
      Array.Clear(userName, 0, userName.Length);
      Array.Clear(domainName, 0, domainName.Length);
    }
  }

  private static unsafe void ZeroFreeCoTaskMem(IntPtr buffer, uint byteCount) {
    if (buffer == IntPtr.Zero) {
      return;
    }

    if (byteCount > 0) {
      new Span<byte>(buffer.ToPointer(), checked((int)byteCount)).Clear();
    }

    Marshal.FreeCoTaskMem(buffer);
  }

  private static string ToStringFromBuffer(char[] buffer) {
    var length = Array.IndexOf(buffer, '\0');
    return new string(buffer, 0, length >= 0 ? length : buffer.Length);
  }

  public sealed class PromptForWindowsCredentialsOptions(string caption, string message) {
    public string Caption { get; } = string.IsNullOrWhiteSpace(caption) ? throw new ArgumentNullException(nameof(caption)) : caption;
    public string Message { get; } = string.IsNullOrWhiteSpace(message) ? throw new ArgumentNullException(nameof(message)) : message;
    public IntPtr HwndParent { get; init; }
    public IntPtr HbmBanner { get; init; }
    public bool IsSaveChecked { get; init; }
    public PromptForWindowsCredentialsFlag Flags { get; init; } = PromptForWindowsCredentialsFlag.CredUiWinGeneric;
    public int AuthErrorCode { get; init; }
  }

  public sealed class PromptCredentialsResult {
    public string UserName { get; init; } = string.Empty;
    public string DomainName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public bool IsSaveChecked { get; init; }
  }
}
