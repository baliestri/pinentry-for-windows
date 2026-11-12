// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;

namespace PinentryForWindows.Ui;

internal sealed class CapturedSecret(byte[] bytes, int length, bool remember) : IDisposable {
  public ReadOnlyMemory<byte> Bytes => bytes.AsMemory(0, length);
  public bool Remember { get; } = remember;

  public void Dispose()
    => CryptographicOperations.ZeroMemory(bytes);
}
