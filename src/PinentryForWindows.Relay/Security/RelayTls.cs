// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Net.Security;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace PinentryForWindows.Relay.Security;

[SupportedOSPlatform("linux")]
internal static class RelayTls {
  public static async Task<SslStream> AuthenticateAsync(Stream transport, RelayIdentity identity,
    X509Certificate2 clientCertificate, CancellationToken ct) {
    var stream = new SslStream(transport, false,
      (_, certificate, _, _) => certificate is not null && identity.IsTrustedBroker(certificate));
    try {
      await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions {
        TargetHost = "pfw-broker",
        ClientCertificates = [clientCertificate],
        EnabledSslProtocols = SslProtocols.Tls13,
        AllowRenegotiation = false,
        CertificateRevocationCheckMode = X509RevocationMode.NoCheck
      }, ct).ConfigureAwait(false);
      return stream;
    }
    catch {
      await stream.DisposeAsync().ConfigureAwait(false);
      throw;
    }
  }
}
