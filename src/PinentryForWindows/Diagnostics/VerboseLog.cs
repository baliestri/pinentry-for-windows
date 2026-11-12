// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Diagnostics;

internal static class VerboseLog {
  private static readonly bool _enabled =
    string.Equals(Environment.GetEnvironmentVariable("PFW_VERBOSE"), "1", StringComparison.Ordinal);

  public static void Write(VerboseEvent eventType) {
    if (!_enabled) {
      return;
    }

    var message = eventType switch {
      VerboseEvent.LocalSessionStarted => "local session started",
      VerboseEvent.CommandStarted => "command started",
      VerboseEvent.BrokerStarted => "broker started",
      VerboseEvent.BrokerWaitingForVm => "waiting for paired WSL VM",
      VerboseEvent.BrokerListening => "broker listener ready",
      VerboseEvent.BrokerPeerAuthenticated => "WSL client authenticated",
      VerboseEvent.PromptQueued => "credential prompt queued",
      VerboseEvent.HelperStarted => "credential helper started",
      VerboseEvent.HelperFinished => "credential helper finished",
      VerboseEvent.HelperFailed => "credential helper failed",
      VerboseEvent.CacheUnavailable => "persistent cache unavailable",
      VerboseEvent.CacheMiss => "cache entry absent",
      VerboseEvent.CacheHit => "cache entry unlocked",
      VerboseEvent.CacheInvalid => "cache entry could not be decrypted",
      VerboseEvent.CacheStored => "cache entry stored",
      VerboseEvent.CacheLookupFailed => "cache lookup failed",
      VerboseEvent.CacheStoreFailed => "cache write failed",
      var _ => "operation"
    };
    try { Console.Error.WriteLine("[pfw:windows] " + message); }
    catch (Exception error) when (error is IOException or ObjectDisposedException) {
      // Optional diagnostics must not interrupt a pinentry request.
    }
  }
}
