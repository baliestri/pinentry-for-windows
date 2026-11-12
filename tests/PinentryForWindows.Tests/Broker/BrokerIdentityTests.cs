// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PinentryForWindows.Broker;

namespace PinentryForWindows.Tests.Broker;

public sealed class BrokerIdentityTests {
  [Fact]
  public void Only_explicitly_paired_client_is_accepted_and_revocation_is_immediate() {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var directory = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      var identity = new BrokerIdentity(directory);
      using var server = identity.GetOrCreateServerCertificate();
      Assert.True(server.HasPrivateKey);

      using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
      var request = new CertificateRequest("CN=pfw-test-client", key, HashAlgorithmName.SHA256);
      using var client = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
      var publicPath = Path.Combine(directory, "client.cer");
      File.WriteAllBytes(publicPath, client.Export(X509ContentType.Cert));

      Assert.Null(identity.OriginFor(client));
      Assert.Throws<ArgumentException>(() => identity.ApproveClient(publicPath, "windows"));
      identity.ApproveClient(publicPath, "wsl:archlinux");
      Assert.StartsWith("wsl:archlinux#", identity.OriginFor(client));
      Assert.Contains("archlinux", identity.PairedDistributions(), StringComparer.Ordinal);
      identity.RevokeClient(client);
      Assert.Null(identity.OriginFor(client));
      Assert.Empty(identity.PairedDistributions());
      identity.ApproveClient(publicPath, "wsl:archlinux");
      identity.Remove();
      Assert.Null(identity.OriginFor(client));
      Assert.True(File.Exists(publicPath)); // An unrelated public file is not broker-owned.
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public void Renewing_the_broker_certificate_keeps_paired_clients() {
    var directory = TestDirectory();
    try {
      var identity = new BrokerIdentity(directory);
      Assert.Null(identity.ServerCertificateExpiry());
      Assert.Throws<InvalidOperationException>(identity.RenewServerCertificate);

      byte[] before;
      using (var server = identity.GetOrCreateServerCertificate()) { before = server.RawData; }

      using var client = CreateClient(directory, "client.cer", out var publicPath);
      identity.ApproveClient(publicPath, "wsl:archlinux");
      identity.RenewServerCertificate();

      using var renewed = identity.GetOrCreateServerCertificate();
      Assert.True(renewed.HasPrivateKey);
      Assert.NotEqual(before, renewed.RawData);
      Assert.Equal(renewed.RawData, File.ReadAllBytes(identity.PublicCertificatePath));
      Assert.StartsWith("wsl:archlinux#", identity.OriginFor(client));
      Assert.True(identity.ServerCertificateExpiry() > DateTime.UtcNow.AddDays(300));
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public void Replacing_a_client_drops_only_the_previous_pairing_of_that_origin() {
    var directory = TestDirectory();
    try {
      var identity = new BrokerIdentity(directory);
      using var previous = CreateClient(directory, "previous.cer", out var previousPath);
      using var other = CreateClient(directory, "other.cer", out var otherPath);
      using var renewed = CreateClient(directory, "renewed.cer", out var renewedPath);
      identity.ApproveClient(previousPath, "wsl:archlinux");
      identity.ApproveClient(otherPath, "wsl:ubuntu");

      identity.ReplaceClient(renewedPath, "wsl:archlinux");
      identity.ReplaceClient(renewedPath, "wsl:archlinux"); // Repeating the pairing is harmless.

      Assert.Null(identity.OriginFor(previous));
      Assert.StartsWith("wsl:archlinux#", identity.OriginFor(renewed));
      Assert.StartsWith("wsl:ubuntu#", identity.OriginFor(other));
      var expiries = identity.ClientCertificateExpiries();
      Assert.Equal(2, expiries.Count);
      Assert.All(expiries, entry => Assert.True(entry.Expiry > DateTime.UtcNow));
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public void Certificate_lifetime_separates_expired_expiring_and_valid() {
    var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    Assert.True(CertificateLifetime.IsExpired(now, now));
    Assert.False(CertificateLifetime.IsExpiringSoon(now, now));
    Assert.True(CertificateLifetime.IsExpiringSoon(now.AddDays(30), now));
    Assert.Equal(29, CertificateLifetime.DaysLeft(now.AddDays(29).AddHours(5), now));
    Assert.False(CertificateLifetime.IsExpiringSoon(now.AddDays(31), now));
    Assert.False(CertificateLifetime.IsExpired(now.AddDays(31), now));
  }

  private static string TestDirectory() {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var directory = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    return directory;
  }

  private static X509Certificate2 CreateClient(string directory, string fileName, out string publicPath) {
    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var request = new CertificateRequest("CN=pfw-test-client", key, HashAlgorithmName.SHA256);
    var client = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    publicPath = Path.Combine(directory, fileName);
    File.WriteAllBytes(publicPath, client.Export(X509ContentType.Cert));
    return client;
  }
}
