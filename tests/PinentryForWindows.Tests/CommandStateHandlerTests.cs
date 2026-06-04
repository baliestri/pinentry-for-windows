// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Commands;
using PinentryForWindows.Tests.TestSupport;
using AssuanLibrary.Server.Abstractions;

namespace PinentryForWindows.Tests;

public sealed class CommandStateHandlerTests {
  [Fact]
  public async Task Text_state_handlers_store_multiword_values() {
    using var _ = SessionStateScope.Create();

    (await new SetTitleCommandHandler().InvokeAsync("SETTITLE Custom Test Title")).ShouldHaveSingleOk();
    (await new SetPromptCommandHandler().InvokeAsync("SETPROMPT Custom PIN Prompt")).ShouldHaveSingleOk();
    (await new SetErrorCommandHandler().InvokeAsync("SETERROR Bad passphrase entered")).ShouldHaveSingleOk();
    (await new SetRepeatCommandHandler().InvokeAsync("SETREPEAT Repeat this passphrase")).ShouldHaveSingleOk();
    (await new SetRepeatErrorCommandHandler().InvokeAsync("SETREPEATERROR Repeat failed badly")).ShouldHaveSingleOk();

    SessionState.Title.ShouldBe("Custom Test Title");
    SessionState.Prompt.ShouldBe("Custom PIN Prompt");
    SessionState.Error.ShouldBe("Bad passphrase entered");
    SessionState.RepeatPassphrase.ShouldBeTrue();
    SessionState.RepeatDescription.ShouldBe("Repeat this passphrase");
    SessionState.RepeatError.ShouldBe("Repeat failed badly");
  }

  [Fact]
  public async Task Button_handlers_translate_underscores_to_ampersands() {
    using var _ = SessionStateScope.Create();

    (await new SetOkButtonCommandHandler().InvokeAsync("SETOK _Unlock now")).ShouldHaveSingleOk();
    (await new SetCancelButtonCommandHandler().InvokeAsync("SETCANCEL _Abort now")).ShouldHaveSingleOk();
    (await new SetNotOkButtonCommandHandler().InvokeAsync("SETNOTOK _Reject now")).ShouldHaveSingleOk();

    SessionState.OkButtonText.ShouldBe("&Unlock now");
    SessionState.CancelButtonText.ShouldBe("&Abort now");
    SessionState.NotOkButtonText.ShouldBe("&Reject now");
  }

  [Fact]
  public async Task SetKeyInfo_sets_and_clears_key_info() {
    using var _ = SessionStateScope.Create();

    (await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO key-1")).ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBe("key-1");

    (await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO --clear")).ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBeEmpty();
  }

  [Fact]
  public async Task SetKeyInfo_rejects_invalid_arity() {
    using var _ = SessionStateScope.Create();

    (await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO a b")).ShouldHaveSingleError(ExitCode.UNKNOW_VALUE);
  }

  [Fact]
  public async Task SetTimeout_sets_valid_integer_and_ignores_invalid_value() {
    using var _ = SessionStateScope.Create();

    (await new SetTimeoutCommandHandler().InvokeAsync("SETTIMEOUT 42")).ShouldHaveSingleOk();
    SessionState.Timeout.ShouldBe(42);

    (await new SetTimeoutCommandHandler().InvokeAsync("SETTIMEOUT invalid")).ShouldHaveSingleOk();
    SessionState.Timeout.ShouldBe(42);
  }

  [Fact]
  public async Task Option_handles_supported_values_and_ignores_unknown_values() {
    using var _ = SessionStateScope.Create();

    (await new OptionCommandHandler().InvokeAsync("OPTION allow-external-password-cache")).ShouldHaveSingleOk();
    (await new OptionCommandHandler().InvokeAsync("OPTION default-ok=_Unlock")).ShouldHaveSingleOk();
    (await new OptionCommandHandler().InvokeAsync("OPTION default-cancel=_Abort")).ShouldHaveSingleOk();
    (await new OptionCommandHandler().InvokeAsync("OPTION default-notok=_Reject")).ShouldHaveSingleOk();
    (await new OptionCommandHandler().InvokeAsync("OPTION default-prompt=PIN:")).ShouldHaveSingleOk();
    (await new OptionCommandHandler().InvokeAsync("OPTION grab")).ShouldHaveSingleOk();
    SessionState.GrabKeyboard.ShouldBeTrue();
    (await new OptionCommandHandler().InvokeAsync("OPTION no-grab")).ShouldHaveSingleOk();
    (await new OptionCommandHandler().InvokeAsync("OPTION unknown-option=ignored")).ShouldHaveSingleOk();

    SessionState.ExternalPassphraseCache.ShouldBeTrue();
    SessionState.OkButtonText.ShouldBe("&Unlock");
    SessionState.CancelButtonText.ShouldBe("&Abort");
    SessionState.NotOkButtonText.ShouldBe("&Reject");
    SessionState.Prompt.ShouldBe("PIN:");
    SessionState.GrabKeyboard.ShouldBeFalse();
  }

  [Fact]
  public async Task Reset_restores_session_defaults() {
    using var _ = SessionStateScope.Create();

    (await new SetTitleCommandHandler().InvokeAsync("SETTITLE Custom Title")).ShouldHaveSingleOk();
    (await new SetDescriptionCommandHandler().InvokeAsync("SETDESC Custom Description")).ShouldHaveSingleOk();
    (await new SetPromptCommandHandler().InvokeAsync("SETPROMPT PIN:")).ShouldHaveSingleOk();
    (await new SetErrorCommandHandler().InvokeAsync("SETERROR Bad PIN")).ShouldHaveSingleOk();
    (await new SetRepeatCommandHandler().InvokeAsync("SETREPEAT Repeat PIN")).ShouldHaveSingleOk();
    (await new OptionCommandHandler().InvokeAsync("OPTION allow-external-password-cache")).ShouldHaveSingleOk();
    (await new OptionCommandHandler().InvokeAsync("OPTION grab")).ShouldHaveSingleOk();
    (await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO key-1")).ShouldHaveSingleOk();
    SessionState.CacheUser = "cache-user";

    (await new ResetCommandHandler().InvokeAsync("RESET")).ShouldHaveSingleOk();

    SessionState.Title.ShouldBe("Pinentry for Windows");
    SessionState.Description.ShouldBe("Enter password for GPG key");
    SessionState.Prompt.ShouldBe("Password:");
    SessionState.CachePrefix.ShouldBe("pfwcache:");
    SessionState.CacheUser.ShouldBeEmpty();
    SessionState.KeyInfo.ShouldBeEmpty();
    SessionState.OkButtonText.ShouldBe("OK");
    SessionState.CancelButtonText.ShouldBe("Cancel");
    SessionState.NotOkButtonText.ShouldBe("Do not do this");
    SessionState.Error.ShouldBeEmpty();
    SessionState.Timeout.ShouldBe(0);
    SessionState.ExternalPassphraseCache.ShouldBeFalse();
    SessionState.RepeatPassphrase.ShouldBeFalse();
    SessionState.RepeatDescription.ShouldBe("Please re-enter your passphrase:");
    SessionState.RepeatError.ShouldBe("Passphrases do not match. Please try again.");
    SessionState.GrabKeyboard.ShouldBeFalse();
  }

  [Theory]
  [InlineData("SETTITLE")]
  [InlineData("SETPROMPT")]
  [InlineData("SETERROR")]
  [InlineData("SETREPEAT")]
  [InlineData("SETREPEATERROR")]
  [InlineData("SETOK")]
  [InlineData("SETCANCEL")]
  [InlineData("SETNOTOK")]
  [InlineData("OPTION")]
  public async Task Missing_required_argument_returns_invalid_number_of_arguments(string commandLine) {
    using var _ = SessionStateScope.Create();
    CommandHandler handler = commandLine switch {
      "SETTITLE" => new SetTitleCommandHandler(),
      "SETPROMPT" => new SetPromptCommandHandler(),
      "SETERROR" => new SetErrorCommandHandler(),
      "SETREPEAT" => new SetRepeatCommandHandler(),
      "SETREPEATERROR" => new SetRepeatErrorCommandHandler(),
      "SETOK" => new SetOkButtonCommandHandler(),
      "SETCANCEL" => new SetCancelButtonCommandHandler(),
      "SETNOTOK" => new SetNotOkButtonCommandHandler(),
      "OPTION" => new OptionCommandHandler(),
      _ => throw new ArgumentOutOfRangeException(nameof(commandLine), commandLine, null)
    };

    (await handler.InvokeAsync(commandLine)).ShouldHaveSingleError(ExitCode.UNKNOW_VALUE);
  }
}
