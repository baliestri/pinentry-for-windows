// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using AssuanLibrary.Server.Abstractions;

namespace PinentryForWindows.Tests.TestSupport;

internal static class CommandHandlerTestExtensions {
  public static async Task<RecordingServerContext> InvokeAsync(this CommandHandler handler, string commandLine) {
    var context = new RecordingServerContext();
    await handler.HandleAsync(AssuanTestCommand.Create(commandLine), context);
    return context;
  }
}
