// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using Moq;
using PinentryForWindows.Commands;
using PinentryForWindows.Runtime;
using PinentryForWindows.Services;
using PinentryForWindows.Tests.TestSupport;

namespace PinentryForWindows.Tests;

public sealed class GetPinCommandHandlerTests {
  [Fact]
  public async Task Cache_hit_returns_password_from_cache_without_prompting() {
    using var _ = SessionStateScope.Create();
    SessionState.ExternalPassphraseCache = true;
    SessionState.KeyInfo = "key-1";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.TryGetCachedAsync("pfwcache:key-1", It.IsAny<CancellationToken>())).ReturnsAsync("cached-secret");
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.TextLines().ShouldBe(["S PASSWORD_FROM_CACHE", "D cached-secret", "OK"]);
    credentials.Verify(service => service.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task Cache_miss_prompts_and_stores_when_external_cache_is_enabled() {
    using var _ = SessionStateScope.Create();
    SessionState.ExternalPassphraseCache = true;
    SessionState.KeyInfo = "key-1";
    SessionState.Description = "Enter PIN";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.TryGetCachedAsync("pfwcache:key-1", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
    credentials
      .Setup(service => service.PromptAsync("Pinentry for Windows", "Enter PIN", "key-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync("secret");
    credentials.Setup(service => service.StoreAsync("pfwcache:key-1", "key-1", "secret", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.TextLines().ShouldBe(["D secret", "OK"]);
    InteractivePromptCoordinator.Current.ShouldBeNull();
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Cache_lookup_failure_falls_back_to_prompting() {
    using var _ = SessionStateScope.Create();
    SessionState.ExternalPassphraseCache = true;
    SessionState.KeyInfo = "key-1";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.TryGetCachedAsync("pfwcache:key-1", It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException());
    credentials
      .Setup(service => service.PromptAsync("Pinentry for Windows", "Enter password for GPG key", "key-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync("secret");
    credentials.Setup(service => service.StoreAsync("pfwcache:key-1", "key-1", "secret", It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.TextLines().ShouldBe(["D secret", "OK"]);
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Empty_cache_value_falls_back_to_prompting() {
    using var _ = SessionStateScope.Create();
    SessionState.ExternalPassphraseCache = true;
    SessionState.KeyInfo = "key-1";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.TryGetCachedAsync("pfwcache:key-1", It.IsAny<CancellationToken>())).ReturnsAsync(string.Empty);
    credentials
      .Setup(service => service.PromptAsync("Pinentry for Windows", "Enter password for GPG key", "key-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync("secret");
    credentials.Setup(service => service.StoreAsync("pfwcache:key-1", "key-1", "secret", It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.TextLines().ShouldBe(["D secret", "OK"]);
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Store_failure_is_ignored_after_successful_prompt() {
    using var _ = SessionStateScope.Create();
    SessionState.ExternalPassphraseCache = true;
    SessionState.KeyInfo = "key-1";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.TryGetCachedAsync("pfwcache:key-1", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
    credentials
      .Setup(service => service.PromptAsync("Pinentry for Windows", "Enter password for GPG key", "key-1", It.IsAny<CancellationToken>()))
      .ReturnsAsync("secret");
    credentials.Setup(service => service.StoreAsync("pfwcache:key-1", "key-1", "secret", It.IsAny<CancellationToken>()))
      .ThrowsAsync(new InvalidOperationException());
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.TextLines().ShouldBe(["D secret", "OK"]);
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Cache_user_takes_precedence_for_prompt_user_but_key_info_remains_cache_key() {
    using var _ = SessionStateScope.Create();
    SessionState.ExternalPassphraseCache = true;
    SessionState.CacheUser = "card user";
    SessionState.KeyInfo = "key-1";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.TryGetCachedAsync("pfwcache:key-1", It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);
    credentials
      .Setup(service => service.PromptAsync("Pinentry for Windows", "Enter password for GPG key", "card user", It.IsAny<CancellationToken>()))
      .ReturnsAsync("secret");
    credentials.Setup(service => service.StoreAsync("pfwcache:key-1", "card user", "secret", It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.TextLines().ShouldBe(["D secret", "OK"]);
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Set_error_skips_cache_includes_error_in_prompt_and_clears_error() {
    using var _ = SessionStateScope.Create();
    SessionState.ExternalPassphraseCache = true;
    SessionState.KeyInfo = "key-1";
    SessionState.Description = "Enter PIN";
    SessionState.Error = "Bad PIN";

    string? promptMessage = null;
    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials
      .Setup(service => service.PromptAsync("Pinentry for Windows", It.IsAny<string>(), "key-1", It.IsAny<CancellationToken>()))
      .Callback<string, string, string, CancellationToken>((_, message, _, _) => promptMessage = message)
      .ReturnsAsync("secret");
    credentials.Setup(service => service.StoreAsync("pfwcache:key-1", "key-1", "secret", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.TextLines().ShouldBe(["D secret", "OK"]);
    promptMessage.ShouldStartWith("Bad PIN");
    promptMessage.ShouldEndWith("Enter PIN");
    SessionState.Error.ShouldBeEmpty();
    credentials.Verify(service => service.TryGetCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Cancelled_prompt_returns_cancelled_error() {
    using var _ = SessionStateScope.Create();

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials
      .Setup(service => service.PromptAsync("Pinentry for Windows", "Enter password for GPG key", "Pinentry for Windows",
        It.IsAny<CancellationToken>()))
      .ReturnsAsync((string?)null);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.ShouldHaveSingleError(83886179);
    InteractivePromptCoordinator.Current.ShouldBeNull();
  }

  [Fact]
  public async Task Repeat_success_returns_pin_repeated_status() {
    using var _ = SessionStateScope.Create();
    SessionState.RepeatPassphrase = true;
    SessionState.RepeatDescription = "Repeat PIN";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials
      .SetupSequence(service => service.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), "Pinentry for Windows", It.IsAny<CancellationToken>()))
      .ReturnsAsync("secret")
      .ReturnsAsync("secret");
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.TextLines().ShouldBe(["S PIN_REPEATED", "D secret", "OK"]);
  }

  [Fact]
  public async Task Repeat_mismatch_warns_and_returns_not_confirmed_error() {
    using var _ = SessionStateScope.Create();
    SessionState.RepeatPassphrase = true;
    SessionState.RepeatDescription = "Repeat PIN";
    SessionState.RepeatError = "Mismatch";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials
      .SetupSequence(service => service.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), "Pinentry for Windows", It.IsAny<CancellationToken>()))
      .ReturnsAsync("secret")
      .ReturnsAsync("different");
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    dialogs.Setup(service => service.ShowWarning("Pinentry for Windows", "Mismatch"));

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.ShouldHaveSingleError(83886194);
    dialogs.VerifyAll();
    credentials.Verify(service => service.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
  }

  [Fact]
  public async Task Repeat_cancelled_second_prompt_returns_cancelled_error() {
    using var _ = SessionStateScope.Create();
    SessionState.RepeatPassphrase = true;
    SessionState.RepeatDescription = "Repeat PIN";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials
      .SetupSequence(service => service.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), "Pinentry for Windows", It.IsAny<CancellationToken>()))
      .ReturnsAsync("secret")
      .ReturnsAsync((string?)null);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);

    var context = await new GetPinCommandHandler(credentials.Object, dialogs.Object).InvokeAsync("GETPIN");

    context.ShouldHaveSingleError(ExitCode.CANCELLED);
    credentials.Verify(service => service.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
  }
}
