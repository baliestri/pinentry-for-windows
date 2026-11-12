// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;

namespace PinentryForWindows.Core.Contracts;

public interface IPinentryCache {
  bool IsAvailable { get; }

  ValueTask<SecretBuffer?> ReadAsync(string origin, string key, CancellationToken ct);

  ValueTask WriteAsync(string origin, string key, ReadOnlyMemory<byte> secret, CancellationToken ct);

  ValueTask RemoveAsync(string origin, string key, CancellationToken ct);
}
