// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Server;

namespace PinentryForWindows.Core.Protocol;

internal static class WireText {
  private static readonly UTF8Encoding _strictUtf8 = new(false, true);

  public static string Decode(ReadOnlyMemory<byte> raw, Encoding? fallbackEncoding = null) {
    var bytes = raw.ToArray();
    try {
      var count = ProtocolEscaping.DecodeDataInPlace(bytes);
      try {
        return _strictUtf8.GetString(bytes, 0, count);
      }
      catch (DecoderFallbackException) when (fallbackEncoding is not null) {
        return fallbackEncoding.GetString(bytes, 0, count);
      }
    }
    catch (Exception error) when (error is ProtocolException or DecoderFallbackException) {
      throw HandlerException.Remote(67109144, "invalid value");
    }
    finally {
      Array.Clear(bytes);
    }
  }

  public static string Require(ReadOnlyMemory<byte> raw, Encoding? fallbackEncoding = null)
    => !raw.IsEmpty
      ? Decode(raw, fallbackEncoding)
      : throw HandlerException.Remote(67109144, "missing value");
}
