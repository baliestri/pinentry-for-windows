// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Services;

internal interface ICredentialService {
  Task<string?> PromptAsync(string title, string message, string userName, CancellationToken ct = default);
  Task<string?> TryGetCachedAsync(string cacheKey, CancellationToken ct = default);
  Task StoreAsync(string cacheKey, string userName, string password, CancellationToken ct = default);
  Task RemoveAsync(string cacheKey, CancellationToken ct = default);
}
