// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Relay.Security;

/// <summary>A pinned certificate is past its validity; the message is fixed text safe to show the user.</summary>
internal sealed class IdentityExpiredException(string message) : Exception(message);
