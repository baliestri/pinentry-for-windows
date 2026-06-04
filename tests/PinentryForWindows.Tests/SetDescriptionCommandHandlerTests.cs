// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Commands;
using PinentryForWindows.Tests.TestSupport;

namespace PinentryForWindows.Tests;

public sealed class SetDescriptionCommandHandlerTests {
  [Fact]
  public async Task Stores_plain_description() {
    using var _ = SessionStateScope.Create();

    (await new SetDescriptionCommandHandler().InvokeAsync("SETDESC Plain description for prompt")).ShouldHaveSingleOk();

    SessionState.Description.ShouldBe("Plain description for prompt");
    SessionState.CacheUser.ShouldBeEmpty();
  }

  [Fact]
  public async Task Extracts_key_id_as_cache_user() {
    using var _ = SessionStateScope.Create();

    (await new SetDescriptionCommandHandler().InvokeAsync("SETDESC Please unlock key ID ABCDEF1234567890")).ShouldHaveSingleOk();

    SessionState.Description.ShouldBe("Please unlock key ID ABCDEF1234567890");
    SessionState.CacheUser.ShouldBe("ABCDEF1234567890");
  }

  [Fact]
  public async Task Extracts_mac_as_cache_user() {
    using var _ = SessionStateScope.Create();

    var mac = "00:11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF";
    (await new SetDescriptionCommandHandler().InvokeAsync($"SETDESC Device {mac}")).ShouldHaveSingleOk();

    SessionState.CacheUser.ShouldBe(mac);
  }

  [Fact]
  public async Task Extracts_smart_card_holder_and_number_and_removes_metadata_from_description() {
    using var _ = SessionStateScope.Create();

    (await new SetDescriptionCommandHandler().InvokeAsync(
      "SETDESC Please unlock the card%0A%0DNumber: 31 184 111%0AHolder: Bruno Sales")).ShouldHaveSingleOk();

    SessionState.Description.ShouldBe("Please unlock the card");
    SessionState.CacheUser.ShouldBe("Bruno Sales (31 184 111)");
  }

  [Fact]
  public async Task Missing_argument_returns_invalid_number_of_arguments() {
    using var _ = SessionStateScope.Create();

    (await new SetDescriptionCommandHandler().InvokeAsync("SETDESC")).ShouldHaveSingleError(ExitCode.UNKNOWN_VALUE);
  }
}
