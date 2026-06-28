// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Commands;
using PinentryForWindows.Tests.TestSupport;

namespace PinentryForWindows.Tests;

public sealed class SetKeyInfoCommandHandlerTests {
  private const string KEYGRIP = "1234567890ABCDEF1234567890ABCDEF12345678";

  [Fact]
  public async Task OpenPGP_key_type_prefix_parses_type_and_keygrip() {
    using var _ = SessionStateScope.Create();

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO n/" + KEYGRIP);

    context.ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBe("n/" + KEYGRIP);
    SessionState.KeyInfoType.ShouldBe("n");
    SessionState.KeyGrip.ShouldBe(KEYGRIP);
  }

  [Fact]
  public async Task SSH_key_type_prefix_parses_type_and_keygrip() {
    using var _ = SessionStateScope.Create();

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO s/" + KEYGRIP);

    context.ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBe("s/" + KEYGRIP);
    SessionState.KeyInfoType.ShouldBe("s");
    SessionState.KeyGrip.ShouldBe(KEYGRIP);
  }

  [Fact]
  public async Task Other_key_type_prefix_parses_type_and_keygrip() {
    using var _ = SessionStateScope.Create();

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO u/" + KEYGRIP);

    context.ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBe("u/" + KEYGRIP);
    SessionState.KeyInfoType.ShouldBe("u");
    SessionState.KeyGrip.ShouldBe(KEYGRIP);
  }

  [Fact]
  public async Task Keygrip_is_stored_as_uppercase() {
    using var _ = SessionStateScope.Create();
    var lowercaseKeygrip = KEYGRIP.ToLowerInvariant();

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO n/" + lowercaseKeygrip);

    context.ShouldHaveSingleOk();
    SessionState.KeyGrip.ShouldBe(KEYGRIP);
  }

  [Fact]
  public async Task Clear_clears_all_key_identity_fields() {
    using var _ = SessionStateScope.Create();
    SessionState.KeyInfo = "n/" + KEYGRIP;
    SessionState.KeyInfoType = "n";
    SessionState.KeyGrip = KEYGRIP;

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO --clear");

    context.ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBeEmpty();
    SessionState.KeyInfoType.ShouldBeEmpty();
    SessionState.KeyGrip.ShouldBeEmpty();
  }

  [Fact]
  public async Task Legacy_raw_keygrip_without_prefix_stores_keyinfo_but_leaves_type_and_grip_empty() {
    using var _ = SessionStateScope.Create();

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO " + KEYGRIP);

    context.ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBe(KEYGRIP);
    SessionState.KeyInfoType.ShouldBeEmpty();
    SessionState.KeyGrip.ShouldBeEmpty();
  }

  [Fact]
  public async Task Unknown_type_prefix_stores_keyinfo_but_leaves_type_and_grip_empty() {
    using var _ = SessionStateScope.Create();

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO x/" + KEYGRIP);

    context.ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBe("x/" + KEYGRIP);
    SessionState.KeyInfoType.ShouldBeEmpty();
    SessionState.KeyGrip.ShouldBeEmpty();
  }

  [Fact]
  public async Task Too_short_hex_after_prefix_stores_keyinfo_but_leaves_type_and_grip_empty() {
    using var _ = SessionStateScope.Create();

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO n/ABCDEF1234");

    context.ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBe("n/ABCDEF1234");
    SessionState.KeyInfoType.ShouldBeEmpty();
    SessionState.KeyGrip.ShouldBeEmpty();
  }

  [Fact]
  public async Task Arbitrary_string_value_stores_keyinfo_but_leaves_type_and_grip_empty() {
    using var _ = SessionStateScope.Create();

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO card-keygrip-1");

    context.ShouldHaveSingleOk();
    SessionState.KeyInfo.ShouldBe("card-keygrip-1");
    SessionState.KeyInfoType.ShouldBeEmpty();
    SessionState.KeyGrip.ShouldBeEmpty();
  }

  [Fact]
  public async Task Missing_argument_returns_error() {
    using var _ = SessionStateScope.Create();

    var context = await new SetKeyInfoCommandHandler().InvokeAsync("SETKEYINFO");

    context.ShouldHaveSingleError(ExitCode.UNKNOWN_VALUE);
  }
}
