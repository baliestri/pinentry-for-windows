// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using PinentryForWindows.Broker;

namespace PinentryForWindows.Tests.Broker;

public sealed class BrokerTlsTests {
  [Theory]
  [InlineData(false)]
  [InlineData(true)]
  public async Task Unpaired_or_revoked_client_cannot_receive_an_assuan_greeting(bool revokeAfterPairing) {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var directory = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      var identity = new BrokerIdentity(directory);
      using var serverCertificate = identity.GetOrCreateServerCertificate();
      using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
      var request = new CertificateRequest("CN=unpaired", key, HashAlgorithmName.SHA256);
      using var candidate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),
        DateTimeOffset.UtcNow.AddMinutes(10));
      using var clientCertificate = X509CertificateLoader.LoadPkcs12(
        candidate.Export(X509ContentType.Pkcs12, string.Empty), string.Empty,
        X509KeyStorageFlags.UserKeySet);
      if (revokeAfterPairing) {
        var publicPath = Path.Combine(directory, "client.cer");
        File.WriteAllBytes(publicPath, candidate.Export(X509ContentType.Cert));
        identity.ApproveClient(publicPath, "wsl:archlinux");
        Assert.NotNull(identity.OriginFor(clientCertificate));
        identity.RevokeClient(clientCertificate);
      }

      Assert.Null(identity.OriginFor(clientCertificate));
      var listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      try {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accepted = listener.AcceptTcpClientAsync(deadline.Token);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
        using var serverSocket = await accepted;
        var serverTask = BrokerTls.AuthenticateAsync(serverSocket.GetStream(), serverCertificate,
          identity, deadline.Token);
        await using var clientStream = new SslStream(client.GetStream(), false, (_, _, _, _) => true);
        _ = await Record.ExceptionAsync(() => clientStream.AuthenticateAsClientAsync(
          new SslClientAuthenticationOptions {
            TargetHost = "pfw-broker", ClientCertificates = [clientCertificate],
            EnabledSslProtocols = SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
          }, deadline.Token));
        await Assert.ThrowsAnyAsync<Exception>(() => serverTask);
      }
      finally { listener.Stop(); }
    }
    finally { Directory.Delete(directory, true); }
  }

  [Fact]
  public async Task Broker_authenticates_paired_origin_before_exchanging_bytes() {
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var directory = Path.Combine(root, "artifacts", "tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    try {
      var identity = new BrokerIdentity(directory);
      using var serverCertificate = identity.GetOrCreateServerCertificate();
      using var importedClient = PairClient(identity, directory);

      var listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      try {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var accepted = listener.AcceptTcpClientAsync(deadline.Token);
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, deadline.Token);
        using var serverSocket = await accepted;
        var serverTask = BrokerTls.AuthenticateAsync(serverSocket.GetStream(), serverCertificate,
          identity, deadline.Token);
        await using var clientStream = new SslStream(client.GetStream(), false,
          (_, remote, _, _) => remote is not null &&
                               CryptographicOperations.FixedTimeEquals(remote.Export(X509ContentType.Cert),
                                 serverCertificate.Export(X509ContentType.Cert)));
        try {
          await clientStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
            TargetHost = "pfw-broker",
            ClientCertificates = [importedClient],
            EnabledSslProtocols = SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.NoCheck
          }, deadline.Token);
        }
        catch (Exception clientError) {
          try { await serverTask; }
          catch (Exception serverError) {
            throw new AggregateException("TLS handshake failed.", clientError, serverError);
          }

          throw;
        }

        await using var serverConnection = await serverTask;
        Assert.StartsWith("wsl:archlinux#", serverConnection.Origin);
        byte[] payload = [0, (byte)'%', 0xff];
        await clientStream.WriteAsync(payload, deadline.Token);
        var received = new byte[payload.Length];
        await serverConnection.Stream.ReadExactlyAsync(received, deadline.Token);
        Assert.Equal(payload, received);
        await serverConnection.Stream.WriteAsync("OK\n"u8.ToArray(), deadline.Token);
        var greeting = new byte[3];
        await clientStream.ReadExactlyAsync(greeting, deadline.Token);
        Assert.Equal("OK\n"u8.ToArray(), greeting);
      }
      finally { listener.Stop(); }
    }
    finally { Directory.Delete(directory, true); }
  }

  private static X509Certificate2 PairClient(BrokerIdentity identity, string directory) {
    using var clientKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var request = new CertificateRequest("CN=paired-wsl", clientKey, HashAlgorithmName.SHA256);
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
      [new Oid("1.3.6.1.5.5.7.3.2")], false));
    using var clientCertificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),
      DateTimeOffset.UtcNow.AddMinutes(10));
    var publicPath = Path.Combine(directory, "client.cer");
    File.WriteAllBytes(publicPath, clientCertificate.Export(X509ContentType.Cert));
    identity.ApproveClient(publicPath, "wsl:archlinux");
    return X509CertificateLoader.LoadPkcs12(
      clientCertificate.Export(X509ContentType.Pkcs12, string.Empty), string.Empty,
      X509KeyStorageFlags.UserKeySet);
  }
}
