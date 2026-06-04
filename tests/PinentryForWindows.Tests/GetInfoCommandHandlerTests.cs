// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Commands;
using PinentryForWindows.Tests.TestSupport;

namespace PinentryForWindows.Tests;

public sealed class GetInfoCommandHandlerTests {
  [Theory]
  [InlineData("GETINFO version")]
  [InlineData("GETINFO pid")]
  [InlineData("GETINFO flavor")]
  [InlineData("GETINFO ttyinfo")]
  public async Task Supported_getinfo_values_return_data_then_ok(string commandLine) {
    using var _ = SessionStateScope.Create();

    var context = await new GetInfoCommandHandler().InvokeAsync(commandLine);

    context.Responses.Count.ShouldBe(2);
    context.Responses[0].Text().ShouldStartWith("D ");
    context.Responses[1].Text().ShouldStartWith("OK");
  }

  [Fact]
  public async Task Flavor_returns_pinentry_for_windows() {
    using var _ = SessionStateScope.Create();

    var context = await new GetInfoCommandHandler().InvokeAsync("GETINFO flavor");

    context.Responses[0].DecodedText().ShouldBe("pinentry-for-windows");
  }

  [Fact]
  public async Task Ttyinfo_returns_windows_console_info() {
    using var _ = SessionStateScope.Create();

    var context = await new GetInfoCommandHandler().InvokeAsync("GETINFO ttyinfo");

    context.Responses[0].DecodedText().ShouldBe("CONOUT$ w32");
  }

  [Theory]
  [InlineData("GETINFO")]
  [InlineData("GETINFO a b")]
  public async Task Invalid_arity_returns_invalid_number_of_arguments(string commandLine) {
    using var _ = SessionStateScope.Create();

    (await new GetInfoCommandHandler().InvokeAsync(commandLine)).ShouldHaveSingleError(ExitCode.UNKNOW_VALUE);
  }

  [Fact]
  public async Task Unsupported_value_returns_unknown_value_error() {
    using var _ = SessionStateScope.Create();

    var context = await new GetInfoCommandHandler().InvokeAsync("GETINFO unsupported-value");

    context.ShouldHaveSingleError(ExitCode.UNKNOW_VALUE);
    context.Responses[0].Text().ShouldContain("unknown value for 'unsupported-value'");
  }
}
