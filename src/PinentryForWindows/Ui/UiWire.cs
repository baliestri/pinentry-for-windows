// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using AssuanLibrary.Protocol;
using PinentryForWindows.Core.Contracts;

namespace PinentryForWindows.Ui;

internal static class UiWire {
  private const int MAX_SECRET_BYTES = 4096;
  private static readonly UTF8Encoding _strictUtf8 = new(false, true);
  private static readonly byte[] _requestMagic = "PFW2"u8.ToArray();
  private static readonly byte[] _resultMagic = "PFR1"u8.ToArray();

  public static async ValueTask WriteRequestAsync(Stream stream, PromptRequest request, CancellationToken ct) {
    if (request.TimeoutSeconds is < 0 or > 300 ||
        request.GetPinTimestamp < 0) {
      throw new ArgumentOutOfRangeException(nameof(request));
    }

    byte[]? title = null;
    byte[]? description = null;
    byte[]? user = null;
    byte[]? prompt = null;
    try {
      title = Encode(request.Title, 256);
      description = Encode(request.Description, 4096);
      user = Encode(request.UserLabel, 256);
      prompt = Encode(request.PromptLabel, 256);

      await stream.WriteAsync(_requestMagic, ct);
      var header = new byte[11];
      header[0] = request.OfferSave ? (byte)1 : (byte)0;
      BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(1), checked((ushort)request.TimeoutSeconds));
      BinaryPrimitives.WriteInt64LittleEndian(header.AsSpan(3), request.GetPinTimestamp);
      await stream.WriteAsync(header, ct);
      await WriteFieldAsync(stream, title, ct);
      await WriteFieldAsync(stream, description, ct);
      await WriteFieldAsync(stream, user, ct);
      await WriteFieldAsync(stream, prompt, ct);
      await stream.FlushAsync(ct);
    }
    finally {
      if (title is not null) {
        CryptographicOperations.ZeroMemory(title);
      }

      if (description is not null) {
        CryptographicOperations.ZeroMemory(description);
      }

      if (user is not null) {
        CryptographicOperations.ZeroMemory(user);
      }

      if (prompt is not null) {
        CryptographicOperations.ZeroMemory(prompt);
      }
    }
  }

  public static async ValueTask<PromptRequest> ReadRequestAsync(Stream stream, CancellationToken ct) {
    var header = new byte[15];
    await stream.ReadExactlyAsync(header, ct);
    if (!header.AsSpan(0, 4).SequenceEqual(_requestMagic) ||
        header[4] > 1) {
      throw new InvalidDataException("Invalid UI request header.");
    }

    var timeout = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(5));
    var timestamp = BinaryPrimitives.ReadInt64LittleEndian(header.AsSpan(7));
    if (timeout > 300 ||
        timestamp < 0) {
      throw new InvalidDataException("Invalid UI timeout.");
    }

    return new PromptRequest(await ReadFieldAsync(stream, 256, ct),
      await ReadFieldAsync(stream, 4096, ct), await ReadFieldAsync(stream, 256, ct),
      await ReadFieldAsync(stream, 256, ct), header[4] == 1, timeout, timestamp);
  }

  public static ValueTask WriteCancelledAsync(Stream stream, CancellationToken ct)
    => WriteResponseAsync(stream, 0, ReadOnlyMemory<byte>.Empty, false, ct);

  public static ValueTask WriteErrorAsync(Stream stream, CancellationToken ct)
    => WriteResponseAsync(stream, 2, ReadOnlyMemory<byte>.Empty, false, ct);

  public static ValueTask WriteTooLongAsync(Stream stream, CancellationToken ct)
    => WriteResponseAsync(stream, 3, ReadOnlyMemory<byte>.Empty, false, ct);

  public static ValueTask WriteResultAsync(Stream stream, ReadOnlyMemory<byte> secret, bool remember,
  CancellationToken ct)
    => WriteResponseAsync(stream, 1, secret, remember, ct);

  public static async ValueTask<PasswordPromptResult?> ReadResultAsync(Stream stream, CancellationToken ct) {
    var header = new byte[8];
    await stream.ReadExactlyAsync(header, ct);
    if (!header.AsSpan(0, 4).SequenceEqual(_resultMagic) ||
        header[5] > 1) {
      throw new InvalidDataException("Invalid UI response header.");
    }

    var length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6));
    if (length > MAX_SECRET_BYTES) {
      throw new InvalidDataException("UI response exceeds the secret limit.");
    }

    if (header[4] == 0 &&
        length == 0) {
      return null;
    }

    if (header[4] == 2 &&
        length == 0) {
      throw new InvalidDataException("UI worker failed.");
    }

    if (header[4] == 3 &&
        length == 0) {
      throw new SecretTooLongException();
    }

    if (header[4] != 1) {
      throw new InvalidDataException("Invalid UI response status.");
    }

    var bytes = new byte[length];
    var secret = new SecretBuffer(MAX_SECRET_BYTES);
    try {
      await stream.ReadExactlyAsync(bytes, ct);
      secret.Append(bytes);
      return new PasswordPromptResult(secret, header[5] == 1);
    }
    catch {
      secret.Dispose();
      throw;
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
  }

  private static async ValueTask WriteResponseAsync(Stream stream, byte status, ReadOnlyMemory<byte> secret,
  bool remember, CancellationToken ct) {
    if (secret.Length > MAX_SECRET_BYTES ||
        (status != 1 && !secret.IsEmpty)) {
      throw new ArgumentOutOfRangeException(nameof(secret));
    }

    var header = new byte[8];
    _resultMagic.CopyTo(header, 0);
    header[4] = status;
    header[5] = remember ? (byte)1 : (byte)0;
    BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), checked((ushort)secret.Length));
    await stream.WriteAsync(header, ct);
    if (!secret.IsEmpty) {
      await stream.WriteAsync(secret, ct);
    }

    await stream.FlushAsync(ct);
  }

  private static byte[] Encode(string text, int maxLength) {
    var bytes = _strictUtf8.GetBytes(text);
    try {
      ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, bytes.Length);
      return bytes;
    }
    catch {
      CryptographicOperations.ZeroMemory(bytes);
      throw;
    }
  }

  private static async ValueTask WriteFieldAsync(Stream stream, byte[] bytes, CancellationToken ct) {
    var length = new byte[2];
    BinaryPrimitives.WriteUInt16LittleEndian(length, checked((ushort)bytes.Length));
    await stream.WriteAsync(length, ct);
    await stream.WriteAsync(bytes, ct);
  }

  private static async ValueTask<string> ReadFieldAsync(Stream stream, int maxLength, CancellationToken ct) {
    var lengthBytes = new byte[2];
    await stream.ReadExactlyAsync(lengthBytes, ct);
    var length = BinaryPrimitives.ReadUInt16LittleEndian(lengthBytes);
    if (length > maxLength) {
      throw new InvalidDataException("UI request exceeds the text limit.");
    }

    var bytes = new byte[length];
    try {
      await stream.ReadExactlyAsync(bytes, ct);
      return _strictUtf8.GetString(bytes);
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
  }
}
