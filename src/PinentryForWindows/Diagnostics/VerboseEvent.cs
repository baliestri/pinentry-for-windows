// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

namespace PinentryForWindows.Diagnostics;

internal enum VerboseEvent {
  LocalSessionStarted,
  CommandStarted,
  BrokerStarted,
  BrokerWaitingForVm,
  BrokerListening,
  BrokerPeerAuthenticated,
  PromptQueued,
  HelperStarted,
  HelperFinished,
  HelperFailed,
  CacheUnavailable,
  CacheMiss,
  CacheHit,
  CacheInvalid,
  CacheStored,
  CacheLookupFailed,
  CacheStoreFailed
}
