// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the zlib/libpng License.
// See the LICENSE file in the repository root for full license text.

using System.Security.Cryptography;

namespace PinentryForWindows.Relay.Relay;

internal static class AssuanRelay {
  public static async Task RunAsync(Stream input, Stream output, Stream secure, CancellationToken ct) {
    var progress = new Progress();
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
    // Console stdin can synchronously wait inside ReadAsync before returning a Task.
    // Never let that prevent the broker greeting from reaching stdout.
    var inbound = Task.Run(() => PumpInputAsync(input, secure, progress, linked.Token), linked.Token);
    var outbound = PumpOutputAsync(secure, output, progress, linked.Token);
    try {
      var first = await Task.WhenAny(inbound, outbound).ConfigureAwait(false);
      await first.ConfigureAwait(false);
    }
    finally {
      await linked.CancelAsync().ConfigureAwait(false);
      await input.DisposeAsync().ConfigureAwait(false);
      await secure.DisposeAsync().ConfigureAwait(false);
      try { await Task.WhenAll(inbound, outbound).WaitAsync(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false); }
      catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or IOException
                                      or TimeoutException) {
        // stdin can ignore cancellation until its OS pipe closes. This process is exiting.
      }
    }
  }

  private static async Task PumpInputAsync(Stream input, Stream secure, Progress progress, CancellationToken ct) {
    var bytes = new byte[4096];
    using var lines = new LineAccumulator();
    try {
      while (true) {
        var count = await input.ReadAsync(bytes, ct).ConfigureAwait(false);
        if (count == 0) {
          break;
        }

        lines.Feed(bytes.AsSpan(0, count), progress.OnInputLine);
        await secure.WriteAsync(bytes.AsMemory(0, count), ct).ConfigureAwait(false);
        await secure.FlushAsync(ct).ConfigureAwait(false);
      }

      lines.Finish();
      await progress.WaitForDrainAsync(ct).ConfigureAwait(false);
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
  }

  private static async Task PumpOutputAsync(Stream secure, Stream output, Progress progress, CancellationToken ct) {
    var bytes = new byte[4096];
    using var lines = new LineAccumulator();
    try {
      while (true) {
        var count = await secure.ReadAsync(bytes, ct).ConfigureAwait(false);
        if (count == 0) {
          break;
        }

        await output.WriteAsync(bytes.AsMemory(0, count), ct).ConfigureAwait(false);
        await output.FlushAsync(ct).ConfigureAwait(false);
        lines.Feed(bytes.AsSpan(0, count), progress.OnOutputLine);
      }

      lines.Finish();
      progress.OutputClosed();
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
  }

  private sealed class Progress {
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _sync = new();
    private bool _greeted;
    private bool _inputClosed;
    private int _pending;

    public void OnInputLine(ReadOnlySpan<byte> line) {
      if (line.IsEmpty ||
          line[0] == (byte)'#' ||
          line is [(byte)'D', (byte)' ', ..] ||
          line.SequenceEqual("END"u8) ||
          line.SequenceEqual("CAN"u8)) {
        return;
      }

      lock (_sync) { _pending++; }
    }

    public void OnOutputLine(ReadOnlySpan<byte> line) {
      lock (_sync) {
        if (!_greeted) {
          if (!line.StartsWith("OK"u8)) {
            throw new InvalidDataException("Invalid broker greeting.");
          }

          _greeted = true;
          if (_inputClosed && _pending == 0) {
            _drained.TrySetResult();
          }

          return;
        }

        if (!line.SequenceEqual("OK"u8) && !line.StartsWith("OK "u8) &&
            !line.SequenceEqual("ERR"u8) && !line.StartsWith("ERR "u8) ||
            _pending <= 0) {
          return;
        }

        _pending--;
        if (_inputClosed && _pending == 0) {
          _drained.TrySetResult();
        }
      }
    }

    public Task WaitForDrainAsync(CancellationToken ct) {
      lock (_sync) {
        _inputClosed = true;
        if (_greeted && _pending == 0) {
          _drained.TrySetResult();
        }
      }

      return _drained.Task.WaitAsync(ct);
    }

    public void OutputClosed() {
      lock (_sync) {
        if (!_greeted) {
          throw new EndOfStreamException("Broker closed before the Assuan greeting.");
        }

        if (_pending > 0) {
          throw new EndOfStreamException("Broker closed during an Assuan command.");
        }
      }
    }
  }

  private delegate void LineHandler(ReadOnlySpan<byte> line);

  private sealed class LineAccumulator : IDisposable {
    private readonly byte[] _line = new byte[1000];
    private int _used;

    public void Dispose()
      => CryptographicOperations.ZeroMemory(_line);

    public void Feed(ReadOnlySpan<byte> bytes, LineHandler handler) {
      foreach (var value in bytes) {
        if (_used == _line.Length) {
          throw new InvalidDataException("Assuan line exceeds 1000 bytes.");
        }

        if (value == (byte)'\n') {
          var length = _used > 0 && _line[_used - 1] == (byte)'\r' ? _used - 1 : _used;
          handler(_line.AsSpan(0, length));
          CryptographicOperations.ZeroMemory(_line);
          _used = 0;
        }
        else {
          _line[_used++] = value;
        }
      }
    }

    public void Finish() {
      if (_used != 0) {
        throw new InvalidDataException("Partial Assuan line at EOF.");
      }
    }
  }
}
