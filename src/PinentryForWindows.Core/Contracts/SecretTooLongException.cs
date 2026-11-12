// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Core.Contracts;

/// <summary>The user entered a passphrase that does not fit the pinentry secret limit.</summary>
public sealed class SecretTooLongException() : Exception("The passphrase exceeds the pinentry secret limit.");
