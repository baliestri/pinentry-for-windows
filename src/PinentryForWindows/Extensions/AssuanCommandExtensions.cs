// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics.CodeAnalysis;
using AssuanLibrary.Protocol.Abstractions;

namespace PinentryForWindows.Extensions;

[ExcludeFromCodeCoverage]
internal static class AssuanCommandExtensions {
  extension(IReadOnlyAssuanCommand command) {
    public string AsText()
      => string.Join(' ', command.Arguments);
  }
}
