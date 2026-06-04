// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using Moq;
using PinentryForWindows.Commands;
using PinentryForWindows.Services;
using PinentryForWindows.Tests.TestSupport;

namespace PinentryForWindows.Tests;

public sealed class ClearPassphraseCommandHandlerTests {
  [Fact]
  public async Task Removes_prefixed_cache_key() {
    using var _ = SessionStateScope.Create();
    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.RemoveAsync("pfwcache:key-1", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

    (await new ClearPassphraseCommandHandler(credentials.Object).InvokeAsync("CLEARPASSPHRASE key-1")).ShouldHaveSingleOk();

    credentials.VerifyAll();
  }

  [Fact]
  public async Task Remove_failure_returns_cache_clear_error() {
    using var _ = SessionStateScope.Create();
    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);
    credentials.Setup(service => service.RemoveAsync("pfwcache:key-1", It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());

    (await new ClearPassphraseCommandHandler(credentials.Object).InvokeAsync("CLEARPASSPHRASE key-1")).ShouldHaveSingleError(1);
  }

  [Theory]
  [InlineData("CLEARPASSPHRASE")]
  [InlineData("CLEARPASSPHRASE a b")]
  public async Task Invalid_arity_returns_invalid_number_of_arguments(string commandLine) {
    using var _ = SessionStateScope.Create();
    var credentials = new Mock<ICredentialService>(MockBehavior.Strict);

    (await new ClearPassphraseCommandHandler(credentials.Object).InvokeAsync(commandLine)).ShouldHaveSingleError(ExitCode.UNKNOW_VALUE);
  }
}
