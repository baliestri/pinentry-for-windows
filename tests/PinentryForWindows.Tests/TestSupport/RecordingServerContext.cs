// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Protocol;
using AssuanLibrary.Server.Abstractions;

namespace PinentryForWindows.Tests.TestSupport;

internal sealed class RecordingServerContext(CancellationToken cancellationToken = default) : IServerContext {
  public List<AssuanResponse> Responses { get; } = [];
  public IServerSession Session { get; } = new RecordingServerSession { CancellationToken = cancellationToken };

  public void SendResponse(AssuanResponseCollection responseCollection)
    => Add(responseCollection);

  public void SendResponse(AssuanResponse response)
    => Responses.Add(response);

  public byte[] Inquire(string keyword, IReadOnlyCollection<string> parameters)
    => throw new NotSupportedException();

  public Task SendResponseAsync(AssuanResponseCollection responseCollection, CancellationToken ct) {
    Add(responseCollection);
    return Task.CompletedTask;
  }

  public Task SendResponseAsync(AssuanResponse response, CancellationToken ct) {
    Responses.Add(response);
    return Task.CompletedTask;
  }

  public Task<ReadOnlyMemory<byte>> InquireAsync(
  string keyword,
  IReadOnlyCollection<string> parameters,
  CancellationToken ct)
    => throw new NotSupportedException();

  private void Add(AssuanResponseCollection responseCollection) {
    foreach (var response in responseCollection) {
      Responses.Add(response);
    }
  }
}
