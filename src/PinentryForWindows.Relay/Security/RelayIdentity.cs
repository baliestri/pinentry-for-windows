// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PinentryForWindows.Relay.Security;

[SupportedOSPlatform("linux")]
internal sealed class RelayIdentity(string directory) {
  public string PrivateKeyPath => Path.Combine(directory, "client.pfx");
  public string PublicCertificatePath => Path.Combine(directory, "client.cer");
  private string BrokerPath => Path.Combine(directory, "broker.cer");

  public void Create() {
    if (!OperatingSystem.IsLinux()) {
      throw new PlatformNotSupportedException();
    }

    Directory.CreateDirectory(directory);
    File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    if (File.Exists(PrivateKeyPath) ||
        File.Exists(PublicCertificatePath)) {
      throw new InvalidOperationException("Relay identity already exists.");
    }

    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var request = new CertificateRequest("CN=pfw-relay", key, HashAlgorithmName.SHA256);
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
    request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.2")], false));

    using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),
      DateTimeOffset.UtcNow.AddYears(1));
    var pkcs12 = certificate.Export(X509ContentType.Pkcs12, string.Empty);

    try {
      File.WriteAllBytes(PrivateKeyPath, pkcs12);
      File.SetUnixFileMode(PrivateKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
      CheckPrivatePermissions();
      File.WriteAllBytes(PublicCertificatePath, certificate.Export(X509ContentType.Cert));
    }
    catch {
      if (File.Exists(PrivateKeyPath)) {
        File.Delete(PrivateKeyPath);
      }

      if (File.Exists(PublicCertificatePath)) {
        File.Delete(PublicCertificatePath);
      }

      throw;
    }
    finally { CryptographicOperations.ZeroMemory(pkcs12); }
  }

  public X509Certificate2 LoadClientCertificate() {
    CheckPrivatePermissions();
    var pkcs12 = File.ReadAllBytes(PrivateKeyPath);
    try {
      return X509CertificateLoader.LoadPkcs12(pkcs12, string.Empty, X509KeyStorageFlags.EphemeralKeySet);
    }
    finally { CryptographicOperations.ZeroMemory(pkcs12); }
  }

  public void TrustBroker(string certificatePath) {
    using var certificate = X509CertificateLoader.LoadCertificateFromFile(certificatePath);
    if (certificate.NotBefore.ToUniversalTime() > DateTime.UtcNow ||
        certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow) {
      throw new InvalidDataException("Broker certificate is expired or not yet valid.");
    }

    Directory.CreateDirectory(directory);
    var publicBytes = certificate.Export(X509ContentType.Cert);
    if (File.Exists(BrokerPath)) {
      if (!CryptographicOperations.FixedTimeEquals(File.ReadAllBytes(BrokerPath), publicBytes)) {
        throw new InvalidOperationException("A different broker identity is already pinned.");
      }

      return;
    }

    File.WriteAllBytes(BrokerPath, publicBytes);
  }

  public bool IsTrustedBroker(X509Certificate certificate) {
    if (!File.Exists(BrokerPath)) {
      return false;
    }

    try {
      using var pinned = X509CertificateLoader.LoadCertificateFromFile(BrokerPath);
      var now = DateTime.UtcNow;
      return pinned.NotBefore.ToUniversalTime() <= now && pinned.NotAfter.ToUniversalTime() > now &&
             CryptographicOperations.FixedTimeEquals(pinned.RawData, certificate.Export(X509ContentType.Cert));
    }
    catch (Exception error) when (error is IOException or CryptographicException) {
      return false;
    }
  }

  public void Remove() {
    foreach (var path in new[] { PrivateKeyPath, PublicCertificatePath, BrokerPath }) {
      if (File.Exists(path)) {
        File.Delete(path);
      }
    }

    if (Directory.Exists(directory) &&
        !Directory.EnumerateFileSystemEntries(directory).Any()) {
      Directory.Delete(directory);
    }
  }

  private void CheckPrivatePermissions() {
    const UnixFileMode MASK = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                              UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
    if ((File.GetUnixFileMode(PrivateKeyPath) & MASK) != UnixFileMode.None) {
      throw new UnauthorizedAccessException("Relay private key is not restricted to the current Linux user.");
    }
  }
}
