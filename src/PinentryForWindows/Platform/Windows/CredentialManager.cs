// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace PinentryForWindows.Platform.Windows;

internal static class CredentialManager {
  public static PromptCredentialsResult? PromptForWindowsCredentials(PromptForWindowsCredentialsOptions options, string userName, string password) {
    var uiInfo = new NativeMethods.CREDUI_INFO {
      pszCaptionText = options.Caption,
      pszMessageText = options.Message,
      hwndParent = options.HwndParent,
      hbmBanner = options.HbmBanner
    };

    var authPackage = 0;
    var save = options.IsSaveChecked;
    var inAuthBuffer = IntPtr.Zero;
    var inAuthBufferSize = 0;
    var outAuthBuffer = IntPtr.Zero;

    try {
      if (!string.IsNullOrEmpty(userName) ||
          !string.IsNullOrEmpty(password)) {
        inAuthBufferSize = GetPackedCredentialSize(userName, password);
        inAuthBuffer = Marshal.AllocCoTaskMem(inAuthBufferSize);

        if (!NativeMethods.CredPackAuthenticationBuffer(NativeMethods.CRED_PACK_GENERIC_CREDENTIALS, userName, password,
              inAuthBuffer, ref inAuthBufferSize)) {
          throw new Win32Exception(Marshal.GetLastWin32Error());
        }
      }

      var result = NativeMethods.CredUIPromptForWindowsCredentials(uiInfo, options.AuthErrorCode, ref authPackage,
        inAuthBuffer, inAuthBufferSize, out outAuthBuffer, out var outAuthBufferSize, ref save, options.Flags);

      return result switch {
        NativeMethods.CredUiPromptReturnCode.Cancelled => null,
        NativeMethods.CredUiPromptReturnCode.Success => UnpackCredentials(outAuthBuffer, outAuthBufferSize, save),
        var _ => throw new Win32Exception((int)result)
      };
    }
    finally {
      if (inAuthBuffer != IntPtr.Zero) {
        Marshal.ZeroFreeCoTaskMemUnicode(inAuthBuffer);
      }

      if (outAuthBuffer != IntPtr.Zero) {
        Marshal.ZeroFreeCoTaskMemUnicode(outAuthBuffer);
      }
    }
  }

  private static int GetPackedCredentialSize(string userName, string password) {
    var size = 0;
    _ = NativeMethods.CredPackAuthenticationBuffer(NativeMethods.CRED_PACK_GENERIC_CREDENTIALS, userName, password,
      IntPtr.Zero, ref size);

    return size > 0 ? size : throw new Win32Exception(Marshal.GetLastWin32Error());
  }

  private static PromptCredentialsResult UnpackCredentials(IntPtr authBuffer, int authBufferSize, bool save) {
    var userNameLength = 0;
    var domainNameLength = 0;
    var passwordLength = 0;

    _ = NativeMethods.CredUnPackAuthenticationBuffer(NativeMethods.CRED_PACK_GENERIC_CREDENTIALS, authBuffer, authBufferSize, null,
      ref userNameLength, null, ref domainNameLength, null, ref passwordLength);

    if (userNameLength <= 0 ||
        passwordLength <= 0) {
      throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    var userName = new StringBuilder(userNameLength);
    var domainName = new StringBuilder(domainNameLength);
    var password = new StringBuilder(passwordLength);

    if (!NativeMethods.CredUnPackAuthenticationBuffer(NativeMethods.CRED_PACK_GENERIC_CREDENTIALS, authBuffer, authBufferSize,
          userName, ref userNameLength, domainName, ref domainNameLength, password, ref passwordLength)) {
      throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    return new PromptCredentialsResult {
      UserName = userName.ToString(),
      DomainName = domainName.ToString(),
      Password = password.ToString(),
      IsSaveChecked = save
    };
  }

  [Flags]
  public enum PromptForWindowsCredentialsFlag {
    CredUiWinGeneric = 0x00000001,
    CredUiWinInCredOnly = 0x00000020
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

  private static class NativeMethods {
    private const string CRED_UI = "credui.dll";
    public const int CRED_PACK_GENERIC_CREDENTIALS = 0x4;

    public enum CredUiPromptReturnCode {
      Success = 0,
      Cancelled = 1223
    }

    [StructLayout(LayoutKind.Sequential)]
    public sealed class CREDUI_INFO {
      public int cbSize = Marshal.SizeOf<CREDUI_INFO>();
      public IntPtr hwndParent;

      [MarshalAs(UnmanagedType.LPWStr)]
      public string? pszMessageText;

      [MarshalAs(UnmanagedType.LPWStr)]
      public string? pszCaptionText;

      public IntPtr hbmBanner;
    }

    [DllImport(CRED_UI, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CredPackAuthenticationBuffer(int flags, string userName, string password, IntPtr packedCredentials,
    ref int packedCredentialsSize);

    [DllImport(CRED_UI, CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CredUnPackAuthenticationBuffer(int flags, IntPtr authBuffer, int authBufferSize, StringBuilder? userName,
    ref int userNameSize, StringBuilder? domainName, ref int domainNameSize, StringBuilder? password, ref int passwordSize);

    [DllImport(CRED_UI, CharSet = CharSet.Unicode)]
    public static extern CredUiPromptReturnCode CredUIPromptForWindowsCredentials(CREDUI_INFO uiInfo, int authError, ref int authPackage,
    IntPtr inAuthBuffer, int inAuthBufferSize, out IntPtr outAuthBuffer, out int outAuthBufferSize, ref bool save,
    PromptForWindowsCredentialsFlag flags);
  }
}
