// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Core.Contracts;

public interface IPinentryUi {
  ValueTask<PasswordPromptResult?> PromptAsync(PromptRequest request, CancellationToken ct);

  ValueTask<bool> ConfirmAsync(DialogRequest request, CancellationToken ct);

  ValueTask MessageAsync(DialogRequest request, CancellationToken ct);
}
