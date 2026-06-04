// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using Moq;
using PinentryForWindows.Commands;
using PinentryForWindows.Services;
using PinentryForWindows.Tests.TestSupport;

namespace PinentryForWindows.Tests;

public sealed class DialogCommandHandlerTests {
  [Fact]
  public async Task Message_uses_command_text_when_present() {
    using var _ = SessionStateScope.Create();
    SessionState.Title = "Title";
    SessionState.Description = "Default description";
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    dialogs.Setup(service => service.ShowMessage("Title", "Command message"));

    (await new MessageCommandHandler(dialogs.Object).InvokeAsync("MESSAGE Command message")).ShouldHaveSingleOk();

    dialogs.VerifyAll();
  }

  [Fact]
  public async Task Message_uses_default_description_without_arguments() {
    using var _ = SessionStateScope.Create();
    SessionState.Title = "Title";
    SessionState.Description = "Default description";
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    dialogs.Setup(service => service.ShowMessage("Title", "Default description"));

    (await new MessageCommandHandler(dialogs.Object).InvokeAsync("MESSAGE")).ShouldHaveSingleOk();

    dialogs.VerifyAll();
  }

  [Fact]
  public async Task Confirm_one_button_shows_message_and_returns_ok() {
    using var _ = SessionStateScope.Create();
    SessionState.Title = "Title";
    SessionState.Description = "Description";
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    dialogs.Setup(service => service.ShowMessage("Title", "Description"));

    (await new ConfirmCommandHandler(dialogs.Object).InvokeAsync("CONFIRM --one-button")).ShouldHaveSingleOk();

    dialogs.VerifyAll();
  }

  [Fact]
  public async Task Confirm_true_returns_ok() {
    using var _ = SessionStateScope.Create();
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    dialogs.Setup(service => service.Confirm("Pinentry for Windows", "Enter password for GPG key", "OK", "Cancel")).Returns(true);

    (await new ConfirmCommandHandler(dialogs.Object).InvokeAsync("CONFIRM")).ShouldHaveSingleOk();
  }

  [Fact]
  public async Task Confirm_uses_configured_button_texts() {
    using var _ = SessionStateScope.Create();
    SessionState.Title = "Title";
    SessionState.Description = "Description";
    SessionState.OkButtonText = "&Approve";
    SessionState.CancelButtonText = "&Deny";
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    dialogs.Setup(service => service.Confirm("Title", "Description", "&Approve", "&Deny")).Returns(true);

    (await new ConfirmCommandHandler(dialogs.Object).InvokeAsync("CONFIRM")).ShouldHaveSingleOk();

    dialogs.VerifyAll();
  }

  [Fact]
  public async Task Confirm_false_returns_cancelled_error() {
    using var _ = SessionStateScope.Create();
    var dialogs = new Mock<IDialogService>(MockBehavior.Strict);
    dialogs.Setup(service => service.Confirm("Pinentry for Windows", "Enter password for GPG key", "OK", "Cancel")).Returns(false);

    (await new ConfirmCommandHandler(dialogs.Object).InvokeAsync("CONFIRM")).ShouldHaveSingleError(ExitCode.CANCELLED);
  }
}
