// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using PinentryForWindows.Relay.Relay;

namespace PinentryForWindows.Relay.Tests;

public sealed class RelayForwardingTests {
  [Fact]
  public async Task Input_eof_during_getpin_keeps_channel_open_until_final_answer() {
    using var input = new MemoryStream("GETPIN\n"u8.ToArray());
    using var output = new MemoryStream();
    await using var server = new DelayedServer();
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await AssuanRelay.RunAsync(input, output, server, deadline.Token);

    Assert.Equal("GETPIN\n", Encoding.UTF8.GetString(server.Received));
    Assert.Equal("OK ready\nD dummy%25secret\nOK\n", Encoding.UTF8.GetString(output.ToArray()));
  }

  [Fact]
  public async Task Broker_close_ends_relay_even_if_stdin_ignores_cancellation() {
    await using var input = new BlockingInput();
    using var output = new MemoryStream();
    await using var server = new DelayedServer("OK\n");
    await AssuanRelay.RunAsync(input, output, server, TestContext.Current.CancellationToken)
      .WaitAsync(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
    Assert.Equal("OK ready\nOK\n", Encoding.UTF8.GetString(output.ToArray()));
  }

  [Fact]
  public async Task Broker_close_before_greeting_is_reported_even_without_a_pending_command() {
    using var input = new MemoryStream();
    using var output = new MemoryStream();
    await using var server = new DelayedServer(greeting: "");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await Assert.ThrowsAsync<EndOfStreamException>(() =>
      AssuanRelay.RunAsync(input, output, server, deadline.Token));
    Assert.Empty(output.ToArray());
  }

  [Fact]
  public async Task Input_eof_waits_for_a_delayed_broker_greeting() {
    using var input = new MemoryStream();
    using var output = new MemoryStream();
    await using var server = new DelayedServer(greetingDelay: TimeSpan.FromMilliseconds(100));
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await AssuanRelay.RunAsync(input, output, server, deadline.Token);
    Assert.Equal("OK ready\n", Encoding.UTF8.GetString(output.ToArray()));
  }

  private sealed class BlockingInput : Stream {
    private readonly TaskCompletionSource<int> _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _reads;
    public override bool CanRead => true;
    public override bool CanWrite => false;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();

    public override long Position {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) {
      if (Interlocked.Increment(ref _reads) != 1) {
        return new ValueTask<int>(_closed.Task);
      }

      "BYE\n"u8.CopyTo(buffer.Span);
      return ValueTask.FromResult("BYE\n"u8.Length);
    }

    protected override void Dispose(bool disposing) {
      _closed.TrySetResult(0);
      base.Dispose(disposing);
    }

    public override void Write(byte[] buffer, int offset, int count)
      => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin)
      => throw new NotSupportedException();

    public override void SetLength(long value)
      => throw new NotSupportedException();
  }

  private sealed class DelayedServer(
    string response = "D dummy%25secret\nOK\n",
    string greeting = "OK ready\n",
    TimeSpan? greetingDelay = null) : Stream {
    private readonly TaskCompletionSource _command = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MemoryStream _input = new();
    private int _readState;
    public byte[] Received => _input.ToArray();
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();

    public override long Position {
      get => throw new NotSupportedException();
      set => throw new NotSupportedException();
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count)
      => throw new NotSupportedException();

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) {
      var state = Interlocked.Increment(ref _readState);
      switch (state) {
        case 1:
          if (greetingDelay is { } delay) {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
          }

          var greetingBytes = Encoding.UTF8.GetBytes(greeting);
          greetingBytes.CopyTo(buffer);
          return greetingBytes.Length;
        case 2:
        {
          await _command.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
          await Task.Delay(50, cancellationToken).ConfigureAwait(false);
          var bytes = Encoding.UTF8.GetBytes(response);
          bytes.CopyTo(buffer);
          return bytes.Length;
        }
        default:
          return 0;
      }
    }

    public override void Write(byte[] buffer, int offset, int count)
      => throw new NotSupportedException();

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
      _input.Write(buffer.Span);
      _command.TrySetResult();
      return ValueTask.CompletedTask;
    }

    public override long Seek(long offset, SeekOrigin origin)
      => throw new NotSupportedException();

    public override void SetLength(long value)
      => throw new NotSupportedException();
  }
}
