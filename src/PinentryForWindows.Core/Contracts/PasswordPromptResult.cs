// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace PinentryForWindows.Core.Contracts;

public sealed class PasswordPromptResult(SecretBuffer secret, bool remember) : IDisposable {
  public SecretBuffer Secret { get; } = secret;
  public bool Remember { get; } = remember;

  public void Dispose()
    => Secret.Dispose();
}
