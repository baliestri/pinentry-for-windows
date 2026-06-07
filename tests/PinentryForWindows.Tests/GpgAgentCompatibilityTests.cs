// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;
using Moq;
using PinentryForWindows.Commands;
using PinentryForWindows.Services;
using PinentryForWindows.Tests.TestSupport;

namespace PinentryForWindows.Tests;

public sealed class GpgAgentCompatibilityTests {
  [Fact]
  public async Task Decrypt_cache_miss_replays_realistic_gpg_agent_sequence() {
    using var _ = SessionStateScope.Create();
    const string KEY_INFO = "1234567890ABCDEF1234567890ABCDEF12345678";

    string? promptMessage = null;
    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.TryGetCachedAsync("pfwcache:" + KEY_INFO, It.IsAny<CancellationToken>()))
      .ReturnsAsync((string?)null);
    credentials.Setup(service => service.PromptAsync("Pinentry", It.IsAny<string>(), "ABCDEF1234567890", It.IsAny<CancellationToken>()))
      .Callback<string, string, string, CancellationToken>((_, message, _, _) => promptMessage = message)
      .ReturnsAsync("decrypt-passphrase");
    credentials.Setup(service => service.StoreAsync("pfwcache:" + KEY_INFO, "ABCDEF1234567890", "decrypt-passphrase", It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    var replay = new GpgAgentReplay(credentials.Object, dialogs.Object);

    await replay.SendOkAsync("OPTION allow-external-password-cache");
    await replay.SendOkAsync("OPTION default-ok=_Unlock");
    await replay.SendOkAsync("OPTION default-cancel=_Cancel");
    await replay.SendOkAsync("SETTITLE Pinentry");
    await replay.SendOkAsync("SETPROMPT Passphrase:");
    await replay.SendOkAsync("SETDESC Please enter the passphrase to unlock the OpenPGP secret key:%0A%22Pinentry Compatibility Decrypt <compat-decrypt@example.invalid>%22%0A2048-bit RSA key, ID ABCDEF1234567890,%0Acreated 2026-06-06.");
    await replay.SendOkAsync("SETKEYINFO " + KEY_INFO);

    var getPin = await replay.SendAsync("GETPIN");

    getPin.TextLines().ShouldBe(["D decrypt-passphrase", "OK"]);
    promptMessage.ShouldNotBeNull();
    promptMessage.ShouldContain("OpenPGP secret key");
    promptMessage.ShouldContain("ABCDEF1234567890");
    SessionState.KeyInfo.ShouldBe(KEY_INFO);
    SessionState.CacheUser.ShouldBe("ABCDEF1234567890");
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Signing_cache_hit_replays_realistic_gpg_agent_sequence() {
    using var _ = SessionStateScope.Create();
    const string KEY_INFO = "89ABCDEF0123456789ABCDEF0123456789ABCDEF";

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.TryGetCachedAsync("pfwcache:" + KEY_INFO, It.IsAny<CancellationToken>()))
      .ReturnsAsync("cached-sign-passphrase");
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    var replay = new GpgAgentReplay(credentials.Object, dialogs.Object);

    await replay.SendOkAsync("OPTION allow-external-password-cache");
    await replay.SendOkAsync("OPTION default-ok=_Sign");
    await replay.SendOkAsync("SETTITLE Pinentry");
    await replay.SendOkAsync("SETPROMPT Passphrase:");
    await replay.SendOkAsync("SETDESC Please enter the passphrase to unlock the OpenPGP secret key:%0A%22Pinentry Compatibility Sign <compat-sign@example.invalid>%22%0A2048-bit RSA key, ID 0123456789ABCDEF,%0Acreated 2026-06-06.");
    await replay.SendOkAsync("SETKEYINFO " + KEY_INFO);

    var getPin = await replay.SendAsync("GETPIN");

    getPin.TextLines().ShouldBe(["S PASSWORD_FROM_CACHE", "D cached-sign-passphrase", "OK"]);
    credentials.Verify(service => service.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
    credentials.Verify(service => service.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Key_generation_repeat_passphrase_sequence_returns_pin_repeated_status() {
    using var _ = SessionStateScope.Create();

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.SetupSequence(service => service.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), "Pinentry", It.IsAny<CancellationToken>()))
      .ReturnsAsync("new-key-passphrase")
      .ReturnsAsync("new-key-passphrase");
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    var replay = new GpgAgentReplay(credentials.Object, dialogs.Object);

    await replay.SendOkAsync("SETTITLE Pinentry");
    await replay.SendOkAsync("SETPROMPT Passphrase:");
    await replay.SendOkAsync("SETDESC Please enter the passphrase to protect your new key");
    await replay.SendOkAsync("SETREPEAT Please re-enter this passphrase");
    await replay.SendOkAsync("SETREPEATERROR The passphrases do not match");

    var getPin = await replay.SendAsync("GETPIN");

    getPin.TextLines().ShouldBe(["S PIN_REPEATED", "D new-key-passphrase", "OK"]);
    credentials.Verify(service => service.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task Smart_card_description_sequence_extracts_metadata_for_cache_user() {
    using var _ = SessionStateScope.Create();
    const string KEY_INFO = "card-keygrip-1";

    string? promptMessage = null;
    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.TryGetCachedAsync("pfwcache:" + KEY_INFO, It.IsAny<CancellationToken>()))
      .ReturnsAsync((string?)null);
    credentials.Setup(service => service.PromptAsync("Pinentry", It.IsAny<string>(), "Ada Lovelace (31 184 111)", It.IsAny<CancellationToken>()))
      .Callback<string, string, string, CancellationToken>((_, message, _, _) => promptMessage = message)
      .ReturnsAsync("card-pin");
    credentials.Setup(service => service.StoreAsync("pfwcache:" + KEY_INFO, "Ada Lovelace (31 184 111)", "card-pin", It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    var replay = new GpgAgentReplay(credentials.Object, dialogs.Object);

    await replay.SendOkAsync("OPTION allow-external-password-cache");
    await replay.SendOkAsync("SETTITLE Pinentry");
    await replay.SendOkAsync("SETDESC Please unlock the card%0A%0ANumber: 31 184 111%0AHolder: Ada Lovelace");
    await replay.SendOkAsync("SETKEYINFO " + KEY_INFO);

    var getPin = await replay.SendAsync("GETPIN");

    getPin.TextLines().ShouldBe(["D card-pin", "OK"]);
    SessionState.Description.ShouldBe("Please unlock the card");
    SessionState.CacheUser.ShouldBe("Ada Lovelace (31 184 111)");
    var smartCardPromptMessage = promptMessage!;
    smartCardPromptMessage.ShouldBe("Please unlock the card");
    smartCardPromptMessage.ShouldNotContain("Number:");
    smartCardPromptMessage.ShouldNotContain("Holder:");
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Clear_passphrase_sequence_removes_prefixed_cache_key() {
    using var _ = SessionStateScope.Create();

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.RemoveAsync("pfwcache:clear-keygrip-1", It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    var replay = new GpgAgentReplay(credentials.Object, dialogs.Object);

    await replay.SendOkAsync("CLEARPASSPHRASE clear-keygrip-1");

    credentials.VerifyAll();
  }

  [Fact]
  public async Task Cancellation_sequence_returns_gpg_agent_cancelled_error() {
    using var _ = SessionStateScope.Create();

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.PromptAsync("Pinentry", "Please enter the passphrase", "Pinentry", It.IsAny<CancellationToken>()))
      .ReturnsAsync((string?)null);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    var replay = new GpgAgentReplay(credentials.Object, dialogs.Object);

    await replay.SendOkAsync("SETTITLE Pinentry");
    await replay.SendOkAsync("SETDESC Please enter the passphrase");

    var getPin = await replay.SendAsync("GETPIN");

    getPin.ShouldHaveSingleError(ExitCode.CANCELLED);
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Retry_after_bad_passphrase_skips_cache_once_and_clears_error() {
    using var _ = SessionStateScope.Create();
    const string KEY_INFO = "retry-keygrip-1";

    string? promptMessage = null;
    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.PromptAsync("Pinentry", It.IsAny<string>(), KEY_INFO, It.IsAny<CancellationToken>()))
      .Callback<string, string, string, CancellationToken>((_, message, _, _) => promptMessage = message)
      .ReturnsAsync("corrected-passphrase");
    credentials.Setup(service => service.StoreAsync("pfwcache:" + KEY_INFO, KEY_INFO, "corrected-passphrase", It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    var replay = new GpgAgentReplay(credentials.Object, dialogs.Object);

    await replay.SendOkAsync("OPTION allow-external-password-cache");
    await replay.SendOkAsync("SETTITLE Pinentry");
    await replay.SendOkAsync("SETKEYINFO " + KEY_INFO);
    await replay.SendOkAsync("SETDESC Please enter the passphrase to unlock the OpenPGP secret key");
    await replay.SendOkAsync("SETERROR Bad passphrase");

    var getPin = await replay.SendAsync("GETPIN");

    getPin.TextLines().ShouldBe(["D corrected-passphrase", "OK"]);
    promptMessage.ShouldNotBeNull();
    promptMessage.ShouldStartWith("Bad passphrase");
    promptMessage.ShouldContain("Please enter the passphrase");
    SessionState.Error.ShouldBeEmpty();
    credentials.Verify(service => service.TryGetCachedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    credentials.VerifyAll();
  }

  [Fact]
  public async Task Repeat_passphrase_mismatch_sequence_returns_not_confirmed_for_retry() {
    using var _ = SessionStateScope.Create();

    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.SetupSequence(service => service.PromptAsync(It.IsAny<string>(), It.IsAny<string>(), "Pinentry", It.IsAny<CancellationToken>()))
      .ReturnsAsync("first-passphrase")
      .ReturnsAsync("different-passphrase");
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    dialogs.Setup(service => service.ShowWarning("Pinentry", "The passphrases do not match"));
    var replay = new GpgAgentReplay(credentials.Object, dialogs.Object);

    await replay.SendOkAsync("SETTITLE Pinentry");
    await replay.SendOkAsync("SETDESC Please enter the passphrase to protect your new key");
    await replay.SendOkAsync("SETREPEAT Please re-enter this passphrase");
    await replay.SendOkAsync("SETREPEATERROR The passphrases do not match");

    var getPin = await replay.SendAsync("GETPIN");

    getPin.ShouldHaveSingleError(ExitCode.NOT_CONFIRMED);
    credentials.Verify(service => service.StoreAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
    dialogs.VerifyAll();
  }

  private sealed class GpgAgentReplay(ICredentialService credentialService, IDialogService dialogService) {
    public async Task<RecordingServerContext> SendAsync(string commandLine) {
      var handler = CreateHandler(commandLine);
      var context = await handler.InvokeAsync(commandLine);
      return context;
    }

    public async Task SendOkAsync(string commandLine) {
      var context = await SendAsync(commandLine);
      context.ShouldHaveSingleOk();
    }

    private CommandHandler CreateHandler(string commandLine)
      => CommandName(commandLine) switch {
        "BYE" => new ByeCommandHandler(),
        "CLEARPASSPHRASE" => new ClearPassphraseCommandHandler(credentialService),
        "CONFIRM" => new ConfirmCommandHandler(dialogService),
        "GETINFO" => new GetInfoCommandHandler(),
        "GETPIN" => new GetPinCommandHandler(credentialService, dialogService),
        "MESSAGE" => new MessageCommandHandler(dialogService),
        "OPTION" => new OptionCommandHandler(),
        "RESET" => new ResetCommandHandler(),
        "SETCANCEL" => new SetCancelButtonCommandHandler(),
        "SETDESC" => new SetDescriptionCommandHandler(),
        "SETERROR" => new SetErrorCommandHandler(),
        "SETKEYINFO" => new SetKeyInfoCommandHandler(),
        "SETNOTOK" => new SetNotOkButtonCommandHandler(),
        "SETOK" => new SetOkButtonCommandHandler(),
        "SETPROMPT" => new SetPromptCommandHandler(),
        "SETREPEAT" => new SetRepeatCommandHandler(),
        "SETREPEATERROR" => new SetRepeatErrorCommandHandler(),
        "SETTIMEOUT" => new SetTimeoutCommandHandler(),
        "SETTITLE" => new SetTitleCommandHandler(),
        var command => throw new NotSupportedException($"Command '{command}' is not supported by the compatibility replay harness.")
      };

    private static string CommandName(string commandLine) {
      var separator = commandLine.IndexOf(' ', StringComparison.Ordinal);
      return separator < 0 ? commandLine : commandLine[..separator];
    }
  }
}
