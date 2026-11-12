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
}
