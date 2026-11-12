// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Diagnostics;

namespace PinentryForWindows.Broker;

/// <summary>
///   Detects WSL utility VM restarts without starting processes. Each VM boot gets a new memory process, so
///   an unchanged snapshot means the VM identifier is still the one the listener was bound to. Hyper-V only
///   reports VM lifecycle events to elevated readers, and a listener bound to a wildcard VM identifier
///   receives no WSL connections without elevation; both were measured before choosing this signal.
/// </summary>
internal static class WslVmPresence {
  // "vmmemWSL" is the Store WSL name; the inbox WSL and other Hyper-V guests appear as "vmmem".
  private static readonly string[] _processNames = ["vmmemWSL", "vmmem"];

  /// <summary>Returns an empty snapshot when no utility VM can be running.</summary>
  public static string Snapshot() {
    var ids = new List<int>();
    foreach (var name in _processNames) {
      foreach (var process in Process.GetProcessesByName(name)) {
        using (process) { ids.Add(process.Id); }
      }
    }

    ids.Sort();
    return string.Join(',', ids);
  }
}
