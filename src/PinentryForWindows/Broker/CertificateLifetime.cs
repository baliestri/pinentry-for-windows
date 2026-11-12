// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Broker;

/// <summary>Classifies a pinned certificate's expiry so renewal is announced before pairing breaks.</summary>
internal static class CertificateLifetime {
  private readonly static TimeSpan WarningWindow = TimeSpan.FromDays(30);

  public static bool IsExpired(DateTime expiryUtc, DateTime nowUtc)
    => expiryUtc <= nowUtc;

  public static bool IsExpiringSoon(DateTime expiryUtc, DateTime nowUtc)
    => !IsExpired(expiryUtc, nowUtc) && expiryUtc - nowUtc <= WarningWindow;

  public static int DaysLeft(DateTime expiryUtc, DateTime nowUtc)
    => Math.Max(0, (int)Math.Floor((expiryUtc - nowUtc).TotalDays));
}
