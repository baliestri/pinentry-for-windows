// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text.RegularExpressions;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;
using AssuanLibrary.Server.Abstractions;
using PinentryForWindows.Extensions;

namespace PinentryForWindows.Commands;

internal sealed partial class SetDescriptionCommandHandler : CommandHandler {
  /// <inheritdoc />
  public override string Name => "SETDESC";

  /// <inheritdoc />
  public override async Task HandleAsync(IReadOnlyAssuanCommand command, IServerContext serverContext) {
    if (command.Arguments.Length is 0) {
      var response = AssuanResponse.Error(ExitCode.UNKNOWN_VALUE, "unknown value for WHAT");
      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      return;
    }

    var description = command.AsText();

    if (string.IsNullOrWhiteSpace(description)) {
      var response = AssuanResponse.Ok();
      await serverContext.SendResponseAsync(response, serverContext.Session.CancellationToken);
      return;
    }

    var cardDescription = ParseCardDescription(description);
    SessionState.Description = cardDescription.Description;

    var keyIdMatch = KeyIdRegex().Match(description);
    if (keyIdMatch.Success) {
      SessionState.CacheUser = keyIdMatch.Groups[1].Value;
    }

    var macMatch = MacRegex().Match(description);
    if (macMatch.Success) {
      SessionState.CacheUser = macMatch.Groups[1].Value;
    }

    if (string.IsNullOrWhiteSpace(SessionState.CacheUser) &&
        !string.IsNullOrWhiteSpace(cardDescription.UserName)) {
      SessionState.CacheUser = cardDescription.UserName;
    }

    var defaultResponse = AssuanResponse.Ok();
    await serverContext.SendResponseAsync(defaultResponse, serverContext.Session.CancellationToken);
  }

  private static CardDescription ParseCardDescription(string description) {
    var normalizedDescription = description.Replace("%0D%0A", "\n", StringComparison.OrdinalIgnoreCase)
      .Replace("%0A", "\n", StringComparison.OrdinalIgnoreCase)
      .Replace("%0D", "\n", StringComparison.OrdinalIgnoreCase);

    var number = string.Empty;
    var holder = string.Empty;
    var descriptionLines = new List<string>();

    foreach (var line in normalizedDescription.ReplaceLineEndings("\n").Split('\n')) {
      var trimmedLine = line.Trim();
      var numberMatch = CardNumberRegex().Match(trimmedLine);
      if (numberMatch.Success) {
        number = numberMatch.Groups[1].Value.Trim();
        continue;
      }

      var holderMatch = CardHolderRegex().Match(trimmedLine);
      if (holderMatch.Success) {
        holder = holderMatch.Groups[1].Value.Trim();
        continue;
      }

      descriptionLines.Add(line);
    }

    var cleanDescription = string.Join(Environment.NewLine, descriptionLines).Trim();
    var userName = BuildCardUserName(holder, number);

    return new CardDescription(
      string.IsNullOrWhiteSpace(cleanDescription) ? description : cleanDescription,
      userName);
  }

  private static string BuildCardUserName(string holder, string number)
    => (holder, number) switch {
      ({ Length: > 0 }, { Length: > 0 }) => $"{holder} ({number})",
      ({ Length: > 0 }, var _) => holder,
      (var _, { Length: > 0 }) => number,
      var _ => string.Empty
    };

  [GeneratedRegex("ID ([0-9a-fA-F]{16})", RegexOptions.Compiled | RegexOptions.Singleline)]
  private static partial Regex KeyIdRegex();

  [GeneratedRegex("((?:[0-9A-Fa-f]{2}:){15}[0-9A-Fa-f]{2})", RegexOptions.Compiled | RegexOptions.Singleline)]
  private static partial Regex MacRegex();

  [GeneratedRegex("^Number:\\s*(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
  private static partial Regex CardNumberRegex();

  [GeneratedRegex("^Holder:\\s*(.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
  private static partial Regex CardHolderRegex();

  private sealed record CardDescription(string Description, string UserName);
}
