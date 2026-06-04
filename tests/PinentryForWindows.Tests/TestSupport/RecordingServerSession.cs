// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;

namespace PinentryForWindows.Tests.TestSupport;

internal sealed class RecordingServerSession : IServerSession {
  public Guid Id { get; } = Guid.NewGuid();
  public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
  public DateTimeOffset LastActivityAt { get; private set; } = DateTimeOffset.UtcNow;
  public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();
  public CancellationToken CancellationToken { get; init; }
  public bool IsClosedGracefully { get; private set; }

  public void RefreshLastActivity()
    => LastActivityAt = DateTimeOffset.UtcNow;

  public void CloseGracefully()
    => IsClosedGracefully = true;

  public void Dispose() { }
}
