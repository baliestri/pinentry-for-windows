// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Transport.Endpoints;
using PinentryForWindows.Runtime;
using PinentryForWindows.Transport;
using PinentryForWindows.Transport.Endpoints;

namespace PinentryForWindows.Tests;

public sealed class TransportTests {
  [Fact]
  public async Task InteractiveCommandSignal_unblocks_sync_and_async_waiters() {
    var signal = new InteractiveCommandSignal();
    var syncWait = Task.Run(signal.Wait, TestContext.Current.CancellationToken);
    var asyncWait = signal.WaitAsync();

    syncWait.IsCompleted.ShouldBeFalse();
    asyncWait.IsCompleted.ShouldBeFalse();

    signal.Set();

    await syncWait.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    await asyncWait.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
  }

  [Fact]
  public async Task ConsoleListener_accepts_one_connection_only() {
    var listener = new ConsoleListener();

    listener.Endpoint.ShouldBe(ConsoleEndpoint.Standard);
    listener.Accept().ShouldNotBeNull();
    Should.Throw<InvalidOperationException>(() => listener.Accept());

    var asyncListener = new ConsoleListener();
    (await asyncListener.AcceptAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    await Should.ThrowAsync<InvalidOperationException>(async () => await asyncListener.AcceptAsync());
  }

  [Fact]
  public void Console_factories_require_console_endpoint() {
    ConsoleListenerFactory.Standard.CreateListener(ConsoleEndpoint.Standard).ShouldBeOfType<ConsoleListener>();
    ConsoleConnectionFactory.Standard.CreateConnection(ConsoleEndpoint.Standard).ShouldBeOfType<ConsoleConnection>();

    Should.Throw<ArgumentException>(() => ConsoleListenerFactory.Standard.CreateListener(new InvalidEndpoint()));
    Should.Throw<ArgumentException>(() => ConsoleConnectionFactory.Standard.CreateConnection(new InvalidEndpoint()));
  }

  [Fact]
  public async Task ConsoleConnection_discard_pending_input_is_noop() {
    var connection = new ConsoleConnection();

    Should.NotThrow(connection.DiscardPendingInput);
    await connection.DiscardPendingInputAsync(TestContext.Current.CancellationToken);
  }

  private readonly record struct InvalidEndpoint : IAssuanEndpoint;
}
