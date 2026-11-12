// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace PinentryForWindows.Broker;

internal static class BrokerTls {
  public static async Task<AuthenticatedBrokerStream> AuthenticateAsync(Stream transport,
    X509Certificate2 serverCertificate, BrokerIdentity identity, CancellationToken ct) {
    string? origin = null;
    var stream = new SslStream(transport, false, (_, certificate, _, _) =>
    {
      if (certificate is null) {
        return false;
      }

      try {
        using var publicCertificate = X509CertificateLoader.LoadCertificate(
          certificate.Export(X509ContentType.Cert));
        origin = identity.OriginFor(publicCertificate);
        return origin is not null;
      }
      catch (Exception) {
        return false;
      }
    });

    try {
      await stream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions {
        ServerCertificate = serverCertificate,
        ClientCertificateRequired = true,
        EnabledSslProtocols = SslProtocols.Tls13,
        AllowRenegotiation = false,
        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
      }, ct).ConfigureAwait(false);

      return origin is not null
        ? new AuthenticatedBrokerStream(stream, origin)
        : throw new AuthenticationException("Client was not paired.");
    }
    catch {
      await stream.DisposeAsync().ConfigureAwait(false);
      throw;
    }
  }
}
