// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Cache;

internal interface IPrfProvider {
  bool IsAvailable { get; }

  ValueTask<byte[]> CreateCredentialAsync(CancellationToken ct);

  ValueTask<byte[]> DeriveAsync(ReadOnlyMemory<byte> credentialId, ReadOnlyMemory<byte> salt, CancellationToken ct);

  ValueTask DeleteCredentialAsync(ReadOnlyMemory<byte> credentialId, CancellationToken ct);
}
