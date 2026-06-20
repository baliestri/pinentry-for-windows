// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Platform.Windows;

namespace PinentryForWindows.Tests;

public sealed class CacheEntryProtectorTests {
  [Fact]
  public void Round_trip_returns_original_passphrase() {
    var passphrase = "correct horse battery staple";
    var cacheKey = "pfwcache:AABBCCDDEEFF0011";

    var stored = CacheEntryProtector.Protect(passphrase, cacheKey);
    var result = CacheEntryProtector.TryUnprotect(stored, cacheKey);

    result.ShouldBe(passphrase);
  }

  [Fact]
  public void Round_trip_works_for_empty_passphrase() {
    var stored = CacheEntryProtector.Protect(string.Empty, "pfwcache:key");
    var result = CacheEntryProtector.TryUnprotect(stored, "pfwcache:key");

    result.ShouldBe(string.Empty);
  }

  [Fact]
  public void Round_trip_works_for_unicode_passphrase() {
    var passphrase = "pässwörd-日本語-🔑";
    var cacheKey = "pfwcache:unicode-key";

    var stored = CacheEntryProtector.Protect(passphrase, cacheKey);
    var result = CacheEntryProtector.TryUnprotect(stored, cacheKey);

    result.ShouldBe(passphrase);
  }

  [Fact]
  public void Stored_value_starts_with_version_prefix() {
    var stored = CacheEntryProtector.Protect("secret", "pfwcache:key");

    stored.ShouldStartWith("pfwv2:");
  }

  [Fact]
  public void Stored_value_is_not_plaintext_passphrase() {
    var passphrase = "my-secret-passphrase";
    var stored = CacheEntryProtector.Protect(passphrase, "pfwcache:key");

    stored.ShouldNotContain(passphrase);
  }

  [Fact]
  public void Domain_separation_different_cache_key_returns_null() {
    var stored = CacheEntryProtector.Protect("secret", "pfwcache:key-A");
    var result = CacheEntryProtector.TryUnprotect(stored, "pfwcache:key-B");

    result.ShouldBeNull();
  }

  [Fact]
  public void Legacy_format_without_prefix_returns_null() {
    var result = CacheEntryProtector.TryUnprotect("plaintext-passphrase", "pfwcache:key");

    result.ShouldBeNull();
  }

  [Fact]
  public void Corrupt_base64_after_prefix_returns_null() {
    var result = CacheEntryProtector.TryUnprotect("pfwv2:!!!not-valid-base64!!!", "pfwcache:key");

    result.ShouldBeNull();
  }

  [Fact]
  public void Truncated_blob_after_prefix_returns_null() {
    var result = CacheEntryProtector.TryUnprotect("pfwv2:YWJj", "pfwcache:key");

    result.ShouldBeNull();
  }

  [Fact]
  public void Empty_string_after_prefix_returns_null() {
    var result = CacheEntryProtector.TryUnprotect("pfwv2:", "pfwcache:key");

    result.ShouldBeNull();
  }
}
