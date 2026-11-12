// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Core.Contracts;

namespace PinentryForWindows.Ui;

internal sealed class OriginLabelUi : IPinentryUi {
  private readonly string _credentialTitle;
  private readonly IPinentryUi _inner;
  private readonly string _prefix;

  public OriginLabelUi(IPinentryUi inner, string authenticatedOrigin) {
    _inner = inner;
    var end = authenticatedOrigin.IndexOf('#');
    if (!authenticatedOrigin.StartsWith("wsl:", StringComparison.Ordinal) ||
        end <= 4) {
      throw new ArgumentException("Invalid authenticated WSL origin.", nameof(authenticatedOrigin));
    }

    var distribution = authenticatedOrigin[4..end];
    if (distribution.Any(character => !char.IsAsciiLetterOrDigit(character) &&
                                      character is not ('.' or '-' or '_'))) {
      throw new ArgumentException("Invalid authenticated WSL distribution.", nameof(authenticatedOrigin));
    }

    _prefix = "WSL " + distribution + " — ";
    _credentialTitle = "Pinentry for Windows [" + distribution + "]";
  }

  public ValueTask<PasswordPromptResult?> PromptAsync(PromptRequest request, CancellationToken ct)
    => _inner.PromptAsync(request with { Title = _credentialTitle }, ct);

  public ValueTask<bool> ConfirmAsync(DialogRequest request, CancellationToken ct)
    => _inner.ConfirmAsync(request with { Title = _prefix + request.Title }, ct);

  public ValueTask MessageAsync(DialogRequest request, CancellationToken ct)
    => _inner.MessageAsync(request with { Title = _prefix + request.Title }, ct);
}
