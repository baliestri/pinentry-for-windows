// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Tests.TestSupport;

namespace PinentryForWindows.Tests;

public sealed class PinentryProcessIntegrationTests {
  [Fact]
  public async Task Process_handles_non_interactive_assuan_commands() {
    await using var session = await PinentryProcessSession.StartAsync();

    await ShouldReturnOk(session, "GETINFO version", "D ");
    await ShouldReturnOk(session, "GETINFO pid", "D ");
    await ShouldReturnOk(session, "GETINFO flavor", "D pinentry-for-windows");
    await ShouldReturnOk(session, "GETINFO ttyinfo", "D CONOUT$ w32");
    await ShouldReturnError(session, "GETINFO unsupported-value");

    await ShouldReturnOk(session, "OPTION allow-external-password-cache");
    await ShouldReturnOk(session, "OPTION default-ok=Unlock");
    await ShouldReturnOk(session, "OPTION default-cancel=Abort");
    await ShouldReturnOk(session, "OPTION default-notok=Reject");
    await ShouldReturnOk(session, "OPTION default-prompt=PIN:");
    await ShouldReturnOk(session, "OPTION grab");
    await ShouldReturnOk(session, "OPTION no-grab");
    await ShouldReturnOk(session, "OPTION unknown-option=ignored");

    await ShouldReturnOk(session, "SETTITLE Pinentry Test Window");
    await ShouldReturnOk(session, "SETPROMPT PIN:");
    await ShouldReturnOk(session, "SETDESC Please unlock key ID ABCDEF1234567890");
    await ShouldReturnOk(session, "SETERROR Bad PIN");
    await ShouldReturnOk(session, "SETREPEAT Repeat PIN");
    await ShouldReturnOk(session, "SETREPEATERROR Passphrases do not match");
    await ShouldReturnOk(session, "SETOK _Unlock");
    await ShouldReturnOk(session, "SETCANCEL _Cancel");
    await ShouldReturnOk(session, "SETNOTOK _Reject");
    await ShouldReturnOk(session, "SETTIMEOUT 1");
    await ShouldReturnOk(session, "SETTIMEOUT invalid");
    await ShouldReturnOk(session, "SETKEYINFO protocol-key");
    await ShouldReturnOk(session, "SETKEYINFO --clear");
    await ShouldReturnOk(session, "RESET");

    await ShouldReturnError(session, "GETINFO");
    await ShouldReturnError(session, "SETTITLE");
    await ShouldReturnError(session, "SETPROMPT");
    await ShouldReturnError(session, "SETDESC");
    await ShouldReturnError(session, "SETERROR");
    await ShouldReturnError(session, "SETREPEAT");
    await ShouldReturnError(session, "SETREPEATERROR");
    await ShouldReturnError(session, "SETOK");
    await ShouldReturnError(session, "SETCANCEL");
    await ShouldReturnError(session, "SETNOTOK");
    await ShouldReturnError(session, "SETKEYINFO a b");

    var bye = await session.SendAsync("BYE");
    bye.Terminal.ShouldStartWith("OK closing connection");
  }

  private static async Task ShouldReturnOk(PinentryProcessSession session, string commandLine, string? requiredText = null) {
    var response = await session.SendAsync(commandLine);
    response.Terminal.ShouldStartWith("OK");

    if (requiredText is not null) {
      response.Text.ShouldContain(requiredText);
    }
  }

  private static async Task ShouldReturnError(PinentryProcessSession session, string commandLine) {
    var response = await session.SendAsync(commandLine);
    response.Terminal.ShouldStartWith("ERR");
  }
}
