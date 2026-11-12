// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Runtime.InteropServices;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace PinentryForWindows;

/// <summary>
///   Serializes child process creation with the lifetime of inheritable handles. Windows hands every
///   inheritable handle to any child started meanwhile, so an unrelated child must never start while the
///   credential helper's pipe ends are still inheritable in this process.
/// </summary>
internal static class ChildProcessGate {
  public static Lock Sync { get; } = new();
}
