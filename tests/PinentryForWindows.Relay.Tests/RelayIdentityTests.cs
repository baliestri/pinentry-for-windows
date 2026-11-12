// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PinentryForWindows.Relay.Security;

namespace PinentryForWindows.Relay.Tests;

public sealed class RelayIdentityTests {
  [Fact]
  public void Client_credentials_are_private_and_broker_pin_rejects_an_impostor() {
    if (!OperatingSystem.IsLinux()) {
      return;
    }

    var directory = Path.Combine("/tmp", "pfw-relay-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      var identity = new RelayIdentity(directory);
      identity.Create();
      using var client = identity.LoadClientCertificate();
      Assert.True(client.HasPrivateKey);
      Assert.Equal(UnixFileMode.None, File.GetUnixFileMode(identity.PrivateKeyPath) &
                                      (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                                       UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute));

      using var firstKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
      var firstRequest = new CertificateRequest("CN=pfw-broker", firstKey, HashAlgorithmName.SHA256);
      using var trusted = firstRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddDays(1));
      var publicPath = Path.Combine(directory, "server.cer");
      File.WriteAllBytes(publicPath, trusted.Export(X509ContentType.Cert));
      identity.TrustBroker(publicPath);
      Assert.True(identity.IsTrustedBroker(trusted));

      using var secondKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
      var secondRequest = new CertificateRequest("CN=pfw-broker", secondKey, HashAlgorithmName.SHA256);
      using var impostor = secondRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),
        DateTimeOffset.UtcNow.AddDays(1));
      Assert.False(identity.IsTrustedBroker(impostor));
      identity.Remove();
      Assert.False(File.Exists(identity.PrivateKeyPath));
      Assert.False(File.Exists(identity.PublicCertificatePath));
    }
    finally {
      if (Directory.Exists(directory)) {
        Directory.Delete(directory, true);
      }
    }
  }

  [Fact]
  public void Renewal_keeps_the_broker_pin_and_a_new_broker_needs_explicit_replacement() {
    if (!OperatingSystem.IsLinux()) {
      return;
    }

    var directory = Path.Combine("/tmp", "pfw-relay-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try { VerifyRenewal(directory); }
    finally {
      if (Directory.Exists(directory)) {
        Directory.Delete(directory, true);
      }
    }
  }

  [SupportedOSPlatform("linux")]
  private static void VerifyRenewal(string directory) {
    var identity = new RelayIdentity(directory);
    Assert.Throws<InvalidOperationException>(identity.Renew);
    identity.Create();
    using var trusted = CreateBroker();
    using var renewedBroker = CreateBroker();
    var trustedPath = Path.Combine(directory, "server.cer");
    var renewedBrokerPath = Path.Combine(directory, "renewed-server.cer");
    File.WriteAllBytes(trustedPath, trusted.Export(X509ContentType.Cert));
    File.WriteAllBytes(renewedBrokerPath, renewedBroker.Export(X509ContentType.Cert));
    identity.TrustBroker(trustedPath);

    // Renewal replaces the client identity, keeps it private and keeps the broker pin.
    var previousCertificate = File.ReadAllBytes(identity.PublicCertificatePath);
    identity.Renew();
    identity.EnsureCurrent();
    using var renewedClient = identity.LoadClientCertificate();
    Assert.True(renewedClient.HasPrivateKey);
    Assert.NotEqual(previousCertificate, File.ReadAllBytes(identity.PublicCertificatePath));
    Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(identity.PrivateKeyPath));
    Assert.True(identity.IsTrustedBroker(trusted));
    Assert.Empty(Directory.EnumerateFiles(directory, "*.new"));

    // A renewed broker is only accepted through an explicit replacement.
    Assert.Throws<InvalidOperationException>(() => identity.TrustBroker(renewedBrokerPath));
    identity.TrustBroker(renewedBrokerPath, true);
    Assert.True(identity.IsTrustedBroker(renewedBroker));
    Assert.False(identity.IsTrustedBroker(trusted));
  }

  private static X509Certificate2 CreateBroker() {
    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var request = new CertificateRequest("CN=pfw-broker", key, HashAlgorithmName.SHA256);
    return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
  }
}
