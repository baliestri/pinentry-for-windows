// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Services;

internal sealed record PromptResult(string Password, bool SaveChecked) {
  public override string ToString()
    => $"PromptResult {{ Password = [redacted], SaveChecked = {SaveChecked} }}";
}
