// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;

namespace PinentryForWindows.Tests.TestSupport;

internal static class ResponseAssertions {
  public static string Text(this AssuanResponse response)
    => response.Type switch {
      AssuanResponseType.Ok => WithPrefix("OK", response.DecodedText()),
      AssuanResponseType.Error => WithPrefix("ERR", response.DecodedText()),
      AssuanResponseType.Status => WithPrefix("S", response.DecodedText()),
      AssuanResponseType.Data => WithPrefix("D", response.DecodedText()),
      AssuanResponseType.Comment => WithPrefix("#", response.DecodedText()),
      AssuanResponseType.Inquire => WithPrefix("INQUIRE", response.DecodedText()),
      _ => response.DecodedText()
    };

  public static string DecodedText(this AssuanResponse response)
    => Encoding.UTF8.GetString(response.DecodedBuffer).TrimEnd('\r', '\n');

  public static IReadOnlyList<string> TextLines(this RecordingServerContext context)
    => context.Responses.Select(response => response.Text()).ToArray();

  public static void ShouldHaveSingleOk(this RecordingServerContext context) {
    context.Responses.Count.ShouldBe(1);
    context.Responses[0].Text().ShouldStartWith("OK");
  }

  public static void ShouldHaveSingleError(this RecordingServerContext context, int code) {
    context.Responses.Count.ShouldBe(1);
    context.Responses[0].Text().ShouldStartWith($"ERR {code}");
  }

  private static string WithPrefix(string prefix, string payload)
    => string.IsNullOrEmpty(payload) ? prefix : $"{prefix} {payload}";
}
