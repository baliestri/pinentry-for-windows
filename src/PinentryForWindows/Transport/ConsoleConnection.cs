// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Client;
using AssuanLibrary.Client.Abstractions;
using AssuanLibrary.Extensions;
using AssuanLibrary.Protocol;
using AssuanLibrary.Transport;
using PinentryForWindows.Runtime;

namespace PinentryForWindows.Transport;

internal sealed class ConsoleConnection : IAssuanConnection {
  private const byte LINE_FEED = 0x0A;
  private bool _eofReceived;

  /// <inheritdoc />
  public bool IsConnected => true;

  /// <inheritdoc />
  public void Open() { }

  /// <inheritdoc />
  public void Write(byte[] buffer)
    => Console.Write(Encoding.UTF8.GetString(buffer));

  /// <inheritdoc />
  public byte[] Read() {
    var input = Console.ReadLine();

    if (input is not null) {
      var received = Encoding.UTF8.GetBytes(input + "\n");
      return received;
    }

    if (_eofReceived) {
      return [];
    }

    _eofReceived = true;

    var promptSignal = InteractivePromptCoordinator.Current;
    if (promptSignal is not null) {
      promptSignal.Wait();
    }

    var byeBytes = "BYE\n"u8.ToArray();
    return byeBytes;
  }

  /// <inheritdoc />
  public byte[] Read(InquireHandler inquireHandler) {
    using var memoryStream = new MemoryStream();

    while (true) {
      var input = Console.ReadLine();
      if (string.IsNullOrWhiteSpace(input)) {
        break;
      }

      var bytes = Encoding.UTF8.GetBytes(input + "\n");
      memoryStream.Write(bytes);

      var response = new AssuanResponse(bytes.Take(LINE_FEED));

      if (response.Type is AssuanResponseType.Ok or AssuanResponseType.Error) {
        break;
      }

      if (response.Type is not AssuanResponseType.Inquire) {
        continue;
      }

      var responseParts = AssuanDecoder.GetInquireParameters(response.Buffer);

      var keyword = responseParts.Length > 0 ? responseParts[0] : string.Empty;
      var parameters = responseParts.Skip(1).ToArray();

      var ctx = new ClientInquireContext(this, keyword, parameters);

      try {
        inquireHandler(ctx);
      }
      catch {
        ctx.Cancel();
        throw;
      }
    }

    return memoryStream.ToArray();
  }

  /// <inheritdoc />
  public byte[] ReadAvailable() {
    var input = Console.ReadLine();

    if (input is not null) {
      var received = Encoding.UTF8.GetBytes(input + "\n");
      return received;
    }

    if (_eofReceived) {
      return [];
    }

    _eofReceived = true;

    var promptSignal = InteractivePromptCoordinator.Current;
    if (promptSignal is not null) {
      promptSignal.Wait();
    }

    var byeBytes = "BYE\n"u8.ToArray();
    return byeBytes;
  }

  /// <inheritdoc />
  public void DiscardPendingInput() { }

  /// <inheritdoc />
  public void Close() { }

  /// <inheritdoc />
  public Task OpenAsync(CancellationToken ct = new())
    => Task.CompletedTask;

  /// <inheritdoc />
  public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = new()) {
    Console.Write(Encoding.UTF8.GetString(buffer.Span));

    return Task.CompletedTask;
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct = new()) {
    var input = Console.ReadLine();

    if (input is not null) {
      var received = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(input + "\n"));
      return received;
    }

    if (_eofReceived) {
      return new ReadOnlyMemory<byte>([]);
    }

    _eofReceived = true;

    var promptSignal = InteractivePromptCoordinator.Current;
    if (promptSignal is not null) {
      await promptSignal.WaitAsync().ConfigureAwait(false);
    }

    var byeBytes = "BYE\n"u8.ToArray();
    return new ReadOnlyMemory<byte>(byeBytes);
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(AsyncInquireHandler inquireHandler, CancellationToken ct = new()) {
    using var memoryStream = new MemoryStream();

    while (!ct.IsCancellationRequested) {
      var input = Console.ReadLine();
      if (string.IsNullOrWhiteSpace(input)) {
        break;
      }

      var bytes = Encoding.UTF8.GetBytes(input + "\n");
      memoryStream.Write(bytes);

      var response = new AssuanResponse(bytes.Take(LINE_FEED));

      if (response.Type is AssuanResponseType.Ok or AssuanResponseType.Error) {
        break;
      }

      if (response.Type is not AssuanResponseType.Inquire) {
        continue;
      }

      var responseParts = AssuanDecoder.GetInquireParameters(response.Buffer);

      var keyword = responseParts.Length > 0 ? responseParts[0] : string.Empty;
      var parameters = responseParts.Skip(1).ToArray();

      var ctx = new ClientInquireContext(this, keyword, parameters);

      try {
        await inquireHandler(ctx, ct);
      }
      catch {
        await ctx.CancelAsync(ct).ConfigureAwait(false);
        throw;
      }
    }

    return memoryStream.ToArray();
  }

  /// <inheritdoc />
  public async ValueTask<ReadOnlyMemory<byte>> ReadAvailableAsync(CancellationToken ct = new()) {
    var input = Console.ReadLine();

    if (input is not null) {
      var received = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(input + "\n"));
      return received;
    }

    if (_eofReceived) {
      return new ReadOnlyMemory<byte>([]);
    }

    _eofReceived = true;

    var promptSignal = InteractivePromptCoordinator.Current;
    if (promptSignal is not null) {
      await promptSignal.WaitAsync().ConfigureAwait(false);
    }

    var byeBytes = "BYE\n"u8.ToArray();
    return new ReadOnlyMemory<byte>(byeBytes);
  }

  /// <inheritdoc />
  public ValueTask DiscardPendingInputAsync(CancellationToken ct = new())
    => ValueTask.CompletedTask;

  /// <inheritdoc />
  public Task CloseAsync(CancellationToken ct = new())
    => Task.CompletedTask;

  /// <inheritdoc />
  public void Dispose() { }

  /// <inheritdoc />
  public ValueTask DisposeAsync()
    => ValueTask.CompletedTask;
}
