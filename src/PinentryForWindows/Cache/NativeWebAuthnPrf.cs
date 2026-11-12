// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Authentication.WebAuthn;

namespace PinentryForWindows.Cache;

internal sealed class NativeWebAuthnPrf : IPrfProvider {
  private const string RP_ID = "pinentry.baliestri.dev";
  private const string RP_NAME = "Pinentry for Windows";
  private const string PUBLIC_KEY = "public-key";
  private const string HASH_NAME = "SHA-256";
  private const string HMAC_SECRET = "hmac-secret";
  private const string CRED_PROTECT = "credProtect";
  private const string USER_NAME = "Pinentry for Windows";
  private const string DISPLAY_NAME = "Pinentry passphrase cache";
  private readonly static Lazy<bool> _available = new(CheckAvailability);

  public bool IsAvailable => _available.Value;

  public ValueTask<byte[]> CreateCredentialAsync(CancellationToken ct)
    => new(Task.Run(() => Create(ct), ct));

  public ValueTask<byte[]> DeriveAsync(ReadOnlyMemory<byte> credentialId, ReadOnlyMemory<byte> salt,
    CancellationToken ct)
    => new(Task.Run(() => Derive(credentialId.ToArray(), salt.ToArray(), ct), ct));

  public ValueTask DeleteCredentialAsync(ReadOnlyMemory<byte> credentialId, CancellationToken ct)
    => new(Task.Run(() => PInvoke.WebAuthNDeletePlatformCredential(credentialId.Span).ThrowOnFailure(), ct));

  private static bool CheckAvailability() {
    try {
      return PInvoke.WebAuthNGetApiVersionNumber() >= 6 &&
             PInvoke.WebAuthNIsUserVerifyingPlatformAuthenticatorAvailable(out var present).Succeeded &&
             present.Value != 0;
    }
    catch (Exception) { return false; }
  }

  private static byte[] ClientData(string type) {
    var challenge = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
      .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    return Encoding.UTF8.GetBytes(
      $"{{\"type\":\"{type}\",\"challenge\":\"{challenge}\",\"origin\":\"https://{RP_ID}\",\"crossOrigin\":false}}");
  }

  private static unsafe byte[] Create(CancellationToken ct) {
    ct.ThrowIfCancellationRequested();
    var userId = RandomNumberGenerator.GetBytes(32);
    var clientData = ClientData("webauthn.create");

    fixed (char* rpId = RP_ID, rpName = RP_NAME, publicKey = PUBLIC_KEY, hash = HASH_NAME,
           hmacId = HMAC_SECRET, protectId = CRED_PROTECT, userName = USER_NAME, displayName = DISPLAY_NAME)
    fixed (byte* userPtr = userId, clientPtr = clientData) {
      var rp = new WEBAUTHN_RP_ENTITY_INFORMATION { dwVersion = 1, pwszId = rpId, pwszName = rpName };
      var user = new WEBAUTHN_USER_ENTITY_INFORMATION {
        dwVersion = 1, cbId = (uint)userId.Length, pbId = userPtr,
        pwszName = userName, pwszDisplayName = displayName
      };
      var algorithm = new WEBAUTHN_COSE_CREDENTIAL_PARAMETER {
        dwVersion = 1, pwszCredentialType = publicKey, lAlg = -7
      };
      var algorithms = new WEBAUTHN_COSE_CREDENTIAL_PARAMETERS {
        cCredentialParameters = 1, pCredentialParameters = &algorithm
      };
      var client = new WEBAUTHN_CLIENT_DATA {
        dwVersion = 1, cbClientDataJSON = (uint)clientData.Length,
        pbClientDataJSON = clientPtr, pwszHashAlgId = hash
      };
      BOOL enabled = true;
      var policy = new WEBAUTHN_CRED_PROTECT_EXTENSION_IN { dwCredProtect = 3, bRequireCredProtect = true };
      var extensions = stackalloc WEBAUTHN_EXTENSION[2];
      extensions[0] = new WEBAUTHN_EXTENSION {
        pwszExtensionIdentifier = hmacId, cbExtension = (uint)sizeof(BOOL), pvExtension = &enabled
      };
      extensions[1] = new WEBAUTHN_EXTENSION {
        pwszExtensionIdentifier = protectId,
        cbExtension = (uint)sizeof(WEBAUTHN_CRED_PROTECT_EXTENSION_IN), pvExtension = &policy
      };
      var options = new WEBAUTHN_AUTHENTICATOR_MAKE_CREDENTIAL_OPTIONS {
        dwVersion = Math.Min(9u, PInvoke.WebAuthNGetApiVersionNumber()),
        dwTimeoutMilliseconds = 120000,
        dwAuthenticatorAttachment = 1,
        bRequireResidentKey = true,
        dwUserVerificationRequirement = 1,
        Extensions = new WEBAUTHN_EXTENSIONS { cExtensions = 2, pExtensions = extensions }
      };

      var cancellationId = CancellationId();
      options.pCancellationId = &cancellationId;
      using var registration = RegisterCancellation(ct, cancellationId);
      var result = PInvoke.WebAuthNAuthenticatorMakeCredential(PInvoke.GetForegroundWindow(),
        in rp, in user, in algorithms, in client, options, out var attestation);
      try {
        result.ThrowOnFailure();
        return CredentialId(attestation);
      }
      finally {
        if (attestation is not null) {
          PInvoke.WebAuthNFreeCredentialAttestation(attestation);
        }
      }
    }
  }

  private static unsafe byte[] Derive(byte[] id, byte[] salt, CancellationToken ct) {
    ct.ThrowIfCancellationRequested();
    if (id.Length is < 1 or > 1024 ||
        salt.Length != 32) {
      throw new ArgumentException("Invalid PRF input.", nameof(id));
    }

    var clientData = ClientData("webauthn.get");
    fixed (char* publicKey = PUBLIC_KEY, hash = HASH_NAME)
    fixed (byte* idPtr = id, saltPtr = salt, clientPtr = clientData) {
      var credential = new WEBAUTHN_CREDENTIAL {
        dwVersion = 1, cbId = (uint)id.Length, pbId = idPtr, pwszCredentialType = publicKey
      };
      var inputSalt = new WEBAUTHN_HMAC_SECRET_SALT {
        cbFirst = (uint)salt.Length, pbFirst = saltPtr
      };
      var saltValues = new WEBAUTHN_HMAC_SECRET_SALT_VALUES { pGlobalHmacSalt = &inputSalt };
      var options = new WEBAUTHN_AUTHENTICATOR_GET_ASSERTION_OPTIONS {
        dwVersion = Math.Min(9u, PInvoke.WebAuthNGetApiVersionNumber()),
        dwTimeoutMilliseconds = 120000,
        dwAuthenticatorAttachment = 1,
        dwUserVerificationRequirement = 1,
        CredentialList = new WEBAUTHN_CREDENTIALS { cCredentials = 1, pCredentials = &credential },
        pHmacSecretSaltValues = &saltValues
      };
      var client = new WEBAUTHN_CLIENT_DATA {
        dwVersion = 1, cbClientDataJSON = (uint)clientData.Length,
        pbClientDataJSON = clientPtr, pwszHashAlgId = hash
      };

      var cancellationId = CancellationId();
      options.pCancellationId = &cancellationId;
      using var registration = RegisterCancellation(ct, cancellationId);
      var result = PInvoke.WebAuthNAuthenticatorGetAssertion(PInvoke.GetForegroundWindow(),
        RP_ID, in client, options, out var assertion);
      try {
        result.ThrowOnFailure();
        return VerifiedPrf(assertion, id);
      }
      finally {
        if (assertion is not null) {
          var secret = assertion->pHmacSecret;
          if (secret is not null &&
              secret->pbFirst is not null &&
              secret->cbFirst == 32) {
            CryptographicOperations.ZeroMemory(new Span<byte>(secret->pbFirst, 32));
          }

          PInvoke.WebAuthNFreeAssertion(assertion);
        }
      }
    }
  }

  private static unsafe byte[] CredentialId(WEBAUTHN_CREDENTIAL_ATTESTATION* attestation) {
    if (attestation is null ||
        attestation->pbCredentialId is null ||
        attestation->cbCredentialId is 0 or > 1024) {
      throw new InvalidDataException("WebAuthn returned an invalid credential ID.");
    }

    return new ReadOnlySpan<byte>(attestation->pbCredentialId, (int)attestation->cbCredentialId).ToArray();
  }

  /// <summary>Returns the PRF output only for a user-verified assertion of the requested credential.</summary>
  private static unsafe byte[] VerifiedPrf(WEBAUTHN_ASSERTION* assertion, byte[] id) {
    if (assertion is null ||
        assertion->pbAuthenticatorData is null ||
        assertion->cbAuthenticatorData < 33 ||
        (assertion->pbAuthenticatorData[32] & 0x04) == 0 ||
        assertion->pHmacSecret is null ||
        assertion->pHmacSecret->pbFirst is null ||
        assertion->pHmacSecret->cbFirst != 32 ||
        assertion->Credential.cbId != id.Length ||
        assertion->Credential.pbId is null ||
        !new ReadOnlySpan<byte>(assertion->Credential.pbId, id.Length).SequenceEqual(id)) {
      throw new InvalidDataException("WebAuthn assertion lacks verified PRF output.");
    }

    return new ReadOnlySpan<byte>(assertion->pHmacSecret->pbFirst, 32).ToArray();
  }

  private static Guid CancellationId() {
    PInvoke.WebAuthNGetCancellationId(out var id).ThrowOnFailure();
    return id;
  }

  private static CancellationTokenRegistration RegisterCancellation(CancellationToken ct, Guid id)
    => ct.Register(static state =>
    {
      var cancellationId = (Guid)state!;
      _ = PInvoke.WebAuthNCancelCurrentOperation(in cancellationId);
    }, id);
}
