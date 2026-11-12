// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PinentryForWindows.Core.Contracts;

namespace PinentryForWindows.Ui;

internal static unsafe partial class NativeCredentialPrompt {
  private const uint CREDUIWIN_GENERIC = 0x1;
  private const uint CREDUIWIN_CHECKBOX = 0x2;
  private const uint CREDUIWIN_IN_CRED_ONLY = 0x20;
  private const uint CRED_PACK_GENERIC_CREDENTIALS = 0x4;
  private const uint ERROR_CANCELLED = 1223;

  public static CapturedSecret? Show(PromptRequest request) {
    var message = BuildMessage(request);
    var authPackage = 0u;
    var save = 0;
    var input = IntPtr.Zero;
    var inputLength = 0u;
    var output = IntPtr.Zero;
    var outputLength = 0u;
    var user = new char[request.UserLabel.Length + 1];
    request.UserLabel.AsSpan().CopyTo(user);
    var emptyPassword = new char[1];
    fixed (char* titlePtr = request.Title)
    fixed (char* messagePtr = message)
    fixed (char* userPtr = user)
    fixed (char* passwordPtr = emptyPassword) {
      var info = new CredUiInfo {
        Size = (uint)sizeof(CredUiInfo), Caption = (IntPtr)titlePtr, Message = (IntPtr)messagePtr
      };
      try {
        input = PackInput(userPtr, passwordPtr, out inputLength);
        var flags = CREDUIWIN_GENERIC | CREDUIWIN_IN_CRED_ONLY |
                    (request.OfferSave ? CREDUIWIN_CHECKBOX : 0);
        var beforeCall = Stopwatch.GetTimestamp();
        uint result;
        try {
          result = CredUiPromptForWindowsCredentialsW(ref info, 0, ref authPackage,
            input, inputLength, out output, out outputLength, ref save, flags);
        }
        finally { PinentryLatency.Record(request.GetPinTimestamp, beforeCall); }

        if (result == ERROR_CANCELLED) {
          return null;
        }

        return result == 0
          ? Unpack(output, outputLength, request.UserLabel, request.OfferSave && save != 0)
          : throw new InvalidOperationException($"Windows Security returned {result}.");
      }
      finally {
        Array.Clear(user);
        Array.Clear(emptyPassword);
        Release(input, inputLength);
        Release(output, outputLength);
      }
    }
  }

  private static IntPtr PackInput(char* user, char* password, out uint length) {
    length = 0;
    _ = CredPackAuthenticationBufferW(CRED_PACK_GENERIC_CREDENTIALS, user, password, null, ref length);
    if (length is 0 or > 4096) {
      throw new InvalidDataException("Invalid credential input size.");
    }

    var input = Marshal.AllocCoTaskMem(checked((int)length));
    if (!CredPackAuthenticationBufferW(CRED_PACK_GENERIC_CREDENTIALS, user, password, (byte*)input, ref length)) {
      var code = Marshal.GetLastPInvokeError();
      Marshal.FreeCoTaskMem(input);
      throw new InvalidOperationException($"Windows Security pack failed: {code}.");
    }

    return input;
  }

  private static void Release(IntPtr buffer, uint length) {
    if (buffer == IntPtr.Zero) {
      return;
    }

    if (length > 0) {
      CryptographicOperations.ZeroMemory(new Span<byte>((void*)buffer, checked((int)length)));
    }

    Marshal.FreeCoTaskMem(buffer);
  }

  internal static string BuildMessage(PromptRequest request)
    => string.IsNullOrWhiteSpace(request.Description) ? request.PromptLabel : request.Description;

  private static CapturedSecret Unpack(IntPtr buffer, uint size, string expectedUser, bool remember) {
    var user = new char[256];
    var domain = new char[256];
    var password = new char[2048];
    var bytes = new byte[4096];
    var transferred = false;
    try {
      var userCount = (uint)user.Length;
      var domainCount = (uint)domain.Length;
      var passwordCount = (uint)password.Length;
      fixed (char* userPtr = user, domainPtr = domain, passwordPtr = password) {
        if (!CredUnPackAuthenticationBufferW(CRED_PACK_GENERIC_CREDENTIALS, buffer, size,
              userPtr, ref userCount, domainPtr, ref domainCount, passwordPtr, ref passwordCount)) {
          throw new InvalidOperationException($"Windows Security unpack failed: {Marshal.GetLastPInvokeError()}.");
        }
      }

      var userEnd = Array.IndexOf(user, '\0');
      var actualUser = user.AsSpan(0, userEnd < 0 ? user.Length : userEnd);
      if (!actualUser.Equals(expectedUser.AsSpan(), StringComparison.OrdinalIgnoreCase)) {
        throw new InvalidDataException("Windows Security returned a different identity.");
      }

      var end = Array.IndexOf(password, '\0');
      var chars = password.AsSpan(0, end < 0 ? password.Length : end);
      // CredUI accepts more UTF-16 text than the secret limit holds once encoded as UTF-8.
      if (Encoding.UTF8.GetByteCount(chars) > bytes.Length) {
        throw new SecretTooLongException();
      }

      var length = Encoding.UTF8.GetBytes(chars, bytes);
      transferred = true;
      return new CapturedSecret(bytes, length, remember);
    }
    finally {
      Array.Clear(user);
      Array.Clear(domain);
      Array.Clear(password);
      if (!transferred) {
        CryptographicOperations.ZeroMemory(bytes);
      }
    }
  }

  [LibraryImport("credui.dll", EntryPoint = "CredUIPromptForWindowsCredentialsW")]
  private static partial uint CredUiPromptForWindowsCredentialsW(ref CredUiInfo info, uint authError,
    ref uint authPackage, IntPtr input, uint inputSize, out IntPtr output, out uint outputSize, ref int save,
    uint flags);

  [LibraryImport("credui.dll", EntryPoint = "CredPackAuthenticationBufferW", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static unsafe partial bool CredPackAuthenticationBufferW(uint flags, char* user, char* password, byte* output,
    ref uint outputSize);

  [LibraryImport("credui.dll", EntryPoint = "CredUnPackAuthenticationBufferW", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  private static unsafe partial bool CredUnPackAuthenticationBufferW(uint flags, IntPtr input, uint inputSize,
    char* user, ref uint userLength, char* domain, ref uint domainLength, char* password, ref uint passwordLength);

  [StructLayout(LayoutKind.Sequential)]
  private struct CredUiInfo {
    public uint Size;
    public IntPtr Parent;
    public IntPtr Message;
    public IntPtr Caption;
    public IntPtr Banner;
  }
}
