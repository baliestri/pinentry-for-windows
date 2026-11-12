// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Core.Contracts;

public sealed record DialogRequest(
  string Title,
  string Description,
  string OkLabel,
  string CancelLabel,
  bool OneButton,
  int TimeoutSeconds
);
