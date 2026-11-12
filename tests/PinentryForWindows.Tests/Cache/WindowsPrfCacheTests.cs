// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using PinentryForWindows.Cache;

namespace PinentryForWindows.Tests.Cache;

public sealed class WindowsPrfCacheTests {
  private const string GRIP = "0123456789ABCDEF0123456789ABCDEF01234567";

  [Fact]
  public async Task Encrypted_entry_roundtrips_only_for_its_origin_and_key() {
    var directory = TestDirectory();
    try {
      var cache = new WindowsPrfCache(directory, new FakePrf());
      var secret = "dummy-%-secret"u8.ToArray();
      await cache.WriteAsync("windows", GRIP, secret, CancellationToken.None);
      using var recovered = await cache.ReadAsync("windows", GRIP, CancellationToken.None);

      Assert.NotNull(recovered);
      Assert.True(recovered.Expose().SequenceEqual(secret));
      Assert.Null(await cache.ReadAsync("wsl:archlinux", GRIP, CancellationToken.None));
      Assert.DoesNotContain(Directory.EnumerateFiles(directory, "*.bin", SearchOption.AllDirectories),
        path => File.ReadAllBytes(path).AsSpan().IndexOf(secret) >= 0);
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Clear_removes_only_the_matching_origin() {
    var directory = TestDirectory();
    try {
      var cache = new WindowsPrfCache(directory, new FakePrf());
      await cache.WriteAsync("windows", GRIP, "one"u8.ToArray(), CancellationToken.None);
      await cache.WriteAsync("wsl:archlinux", GRIP, "two"u8.ToArray(), CancellationToken.None);
      await cache.RemoveAsync("windows", GRIP, CancellationToken.None);

      Assert.Null(await cache.ReadAsync("windows", GRIP, CancellationToken.None));
      using var other = await cache.ReadAsync("wsl:archlinux", GRIP, CancellationToken.None);
      Assert.NotNull(other);
      Assert.True(other.Expose().SequenceEqual("two"u8));
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Modified_ciphertext_is_not_returned_as_a_cached_password() {
    var directory = TestDirectory();
    try {
      var cache = new WindowsPrfCache(directory, new FakePrf());
      await cache.WriteAsync("windows", GRIP, "dummy"u8.ToArray(), CancellationToken.None);
      var entry = Directory.EnumerateFiles(Path.Combine(directory, "entries"), "*.bin").Single();
      var bytes = File.ReadAllBytes(entry);
      bytes[^1] ^= 1;
      File.WriteAllBytes(entry, bytes);

      Assert.Null(await cache.ReadAsync("windows", GRIP, CancellationToken.None));
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Clear_all_removes_local_files_when_the_hello_credential_is_gone() {
    var directory = TestDirectory();
    try {
      await new WindowsPrfCache(directory, new FakePrf()).WriteAsync("windows", GRIP, "one"u8.ToArray(),
        CancellationToken.None);
      var orphaned = new WindowsPrfCache(directory, new FakePrf { DeleteFails = true });

      Assert.False(await orphaned.ClearAllAsync(CancellationToken.None));
      Assert.False(File.Exists(Path.Combine(directory, "hello.cred")));
      Assert.Empty(Directory.EnumerateFiles(Path.Combine(directory, "entries"), "*.bin"));

      // The cache must be usable again with a fresh credential.
      await orphaned.WriteAsync("windows", GRIP, "two"u8.ToArray(), CancellationToken.None);
      using var recovered = await orphaned.ReadAsync("windows", GRIP, CancellationToken.None);
      Assert.NotNull(recovered);
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Clear_all_removes_local_files_when_the_credential_record_is_damaged() {
    var directory = TestDirectory();
    try {
      var cache = new WindowsPrfCache(directory, new FakePrf());
      await cache.WriteAsync("windows", GRIP, "one"u8.ToArray(), CancellationToken.None);
      File.WriteAllBytes(Path.Combine(directory, "hello.cred"), [0xff]);

      Assert.False(await cache.ClearAllAsync(CancellationToken.None));
      Assert.False(File.Exists(Path.Combine(directory, "hello.cred")));
      Assert.Empty(Directory.EnumerateFiles(Path.Combine(directory, "entries"), "*.bin"));
      Assert.True(await cache.ClearAllAsync(CancellationToken.None));
    }
    finally { Directory.Delete(directory, true); }
  }

  private static string TestDirectory() {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var directory = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    return directory;
  }

  private sealed class FakePrf : IPrfProvider {
    public bool DeleteFails { get; init; }
    public bool IsAvailable => true;

    public ValueTask<byte[]> CreateCredentialAsync(CancellationToken ct)
      => ValueTask.FromResult(new byte[] { 1, 2, 3 });

    public ValueTask<byte[]> DeriveAsync(ReadOnlyMemory<byte> credentialId,
      ReadOnlyMemory<byte> salt, CancellationToken ct)
      => ValueTask.FromResult(HMACSHA256.HashData(credentialId.Span, salt.Span));

    public ValueTask DeleteCredentialAsync(ReadOnlyMemory<byte> credentialId, CancellationToken ct)
      => DeleteFails
        ? ValueTask.FromException(new InvalidOperationException("Credential not found."))
        : ValueTask.CompletedTask;
  }
}
