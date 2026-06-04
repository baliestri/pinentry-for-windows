// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using PinentryForWindows.Extensions;
using PinentryForWindows.Tests.TestSupport;

namespace PinentryForWindows.Tests;

public sealed class AssuanCommandArgumentsTests {
  [Fact]
  public void Text_recomposes_split_arguments_with_spaces() {
    var command = AssuanTestCommand.Create("SETDESC Please unlock key ID ABCDEF1234567890");

    command.AsText().ShouldBe("Please unlock key ID ABCDEF1234567890");
  }
}
