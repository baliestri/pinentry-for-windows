// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Services;

namespace PinentryForWindows.Tests;

public sealed class PromptResultTests {
  [Fact]
  public void ToString_does_not_include_password_value() {
    var result = new PromptResult("supersecretpassphrase", false);

    result.ToString().ShouldNotContain("supersecretpassphrase");
  }

  [Fact]
  public void ToString_includes_redacted_placeholder() {
    var result = new PromptResult("any-password", true);

    result.ToString().ShouldContain("[redacted]");
  }

  [Fact]
  public void ToString_includes_save_checked_state() {
    var trueResult = new PromptResult("pw", true);
    var falseResult = new PromptResult("pw", false);

    trueResult.ToString().ShouldContain("True");
    falseResult.ToString().ShouldContain("False");
  }

  [Fact]
  public void Password_property_is_still_accessible_directly() {
    var result = new PromptResult("mysecret", false);

    result.Password.ShouldBe("mysecret");
  }

  [Fact]
  public void Record_equality_is_based_on_password_value_not_redacted_representation() {
    var a = new PromptResult("pw", true);
    var b = new PromptResult("pw", true);
    var c = new PromptResult("different", true);

    a.ShouldBe(b);
    a.ShouldNotBe(c);
  }
}
