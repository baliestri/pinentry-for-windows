// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;
using System.Runtime.InteropServices;
using PinentryForWindows.Core.Contracts;
using PinentryForWindows.Ui;

namespace PinentryForWindows.Tests.Ui;

public sealed class UiWireTests {
  [Fact]
  public async Task Secret_result_preserves_binary_bytes_and_remember_choice() {
    using var stream = new MemoryStream();
    byte[] expected = [0, (byte)'%', (byte)'\n', 0xff, (byte)'A'];
    await UiWire.WriteResultAsync(stream, expected, true, CancellationToken.None);
    stream.Position = 0;

    using var result = await UiWire.ReadResultAsync(stream, CancellationToken.None);
    Assert.NotNull(result);
    Assert.True(result.Remember);
    Assert.True(result.Secret.Expose().SequenceEqual(expected));
  }

  [Fact]
  public async Task Oversized_child_response_is_rejected_before_read() {
    byte[] oversizedHeader = [(byte)'P', (byte)'F', (byte)'R', (byte)'1', 1, 0, 0x01, 0x10];
    using var stream = new MemoryStream(oversizedHeader);
    await Assert.ThrowsAsync<InvalidDataException>(async () =>
      await UiWire.ReadResultAsync(stream, CancellationToken.None));
  }

  [Fact]
  public async Task Cancelled_child_does_not_return_a_password() {
    using var stream = new MemoryStream();
    await UiWire.WriteCancelledAsync(stream, CancellationToken.None);
    stream.Position = 0;
    Assert.Null(await UiWire.ReadResultAsync(stream, CancellationToken.None));
  }

  [Fact]
  public async Task Oversized_passphrase_is_reported_as_too_long_instead_of_a_helper_failure() {
    using var stream = new MemoryStream();
    await UiWire.WriteTooLongAsync(stream, CancellationToken.None);
    stream.Position = 0;
    await Assert.ThrowsAsync<SecretTooLongException>(async () =>
      await UiWire.ReadResultAsync(stream, CancellationToken.None));
  }

  [Fact]
  public async Task Prompt_description_is_bounded_before_launching_ui() {
    using var stream = new MemoryStream();
    var request = new PromptRequest("Title", new string('X', 5000), "User", "Password:", false, 0);
    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
      await UiWire.WriteRequestAsync(stream, request, CancellationToken.None));
  }

  [Fact]
  public async Task Prompt_request_preserves_getpin_timestamp_across_the_helper_pipe() {
    using var stream = new MemoryStream();
    var timestamp = Stopwatch.GetTimestamp();
    var request = new PromptRequest("Title", "Description", "User", "Password:", false, 0, timestamp);
    await UiWire.WriteRequestAsync(stream, request, CancellationToken.None);
    stream.Position = 0;

    var decoded = await UiWire.ReadRequestAsync(stream, CancellationToken.None);
    Assert.Equal(timestamp, decoded.GetPinTimestamp);
    Assert.Equal("Description", decoded.Description);
  }

  [Fact]
  public async Task Request_text_buffers_are_wiped_after_serialization() {
    await using var stream = new RecordingRequestStream();
    var request = new PromptRequest("Sensitive title text", "Sensitive key description",
      "User label 12345", "Password prompt 123", false, 0);

    await UiWire.WriteRequestAsync(stream, request, CancellationToken.None);

    Assert.Equal(4, stream.BorrowedTextBuffers.Count);
    Assert.All(stream.BorrowedTextBuffers, bytes => Assert.All(bytes, value => Assert.Equal(0, value)));
    stream.Position = 0;
    Assert.Equal(request.Description, (await UiWire.ReadRequestAsync(stream, CancellationToken.None)).Description);
  }

  [Fact]
  public async Task Request_text_buffers_are_wiped_when_the_pipe_fails() {
    await using var stream = new RecordingRequestStream { FailOnTextField = 2 };
    var request = new PromptRequest("Sensitive title text", "Sensitive key description",
      "User label 12345", "Password prompt 123", false, 0);

    await Assert.ThrowsAsync<IOException>(async () =>
      await UiWire.WriteRequestAsync(stream, request, CancellationToken.None));
    Assert.Equal(2, stream.BorrowedTextBuffers.Count);
    Assert.All(stream.BorrowedTextBuffers, bytes => Assert.All(bytes, value => Assert.Equal(0, value)));
  }

  private sealed class RecordingRequestStream : MemoryStream {
    private int _textFields;
    public List<byte[]> BorrowedTextBuffers { get; } = [];
    public int FailOnTextField { get; init; }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
      if (buffer.Length > 11 &&
          MemoryMarshal.TryGetArray(buffer, out var segment) &&
          segment.Array is { } bytes) {
        BorrowedTextBuffers.Add(bytes);
        if (++_textFields == FailOnTextField) {
          throw new IOException("Simulated pipe failure.");
        }
      }

      return base.WriteAsync(buffer, cancellationToken);
    }
  }
}
