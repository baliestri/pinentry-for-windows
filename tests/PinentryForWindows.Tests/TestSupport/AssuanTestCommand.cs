// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Text;
using AssuanLibrary.Protocol;
using AssuanLibrary.Protocol.Abstractions;

namespace PinentryForWindows.Tests.TestSupport;

internal static class AssuanTestCommand {
  public static IReadOnlyAssuanCommand Create(string commandLine)
    => new AssuanCommand(Encoding.UTF8.GetBytes(commandLine + "\n"));
}
