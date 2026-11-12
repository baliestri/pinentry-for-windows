// Copyright (c) Bruno Sales <me@baliestri.dev>. Licensed under the MIT License.
// See the LICENSE file in the repository root for full license text.

using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using PinentryForWindows.Core.Contracts;
using PinentryForWindows.Core.Protocol;
using PinentryForWindows.Diagnostics;
using PinentryForWindows.Ui;

namespace PinentryForWindows.Broker;

internal sealed class BrokerHost(BrokerIdentity identity, IPinentryUi ui, IPinentryCache cache, string productVersion) {
  private const int MAX_SESSIONS = 8;
  private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);

  // A VM without the paired distribution running needs wsl.exe to notice it starting; the relay waits 15 s.
  private static readonly TimeSpan _distributionInterval = TimeSpan.FromSeconds(5);

  public async Task RunAsync(CancellationToken ct) {
    using var registration = Registry.LocalMachine
      .OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Virtualization\GuestCommunicationServices\" +
                  HyperVEndpoint.ServiceId);

    if (registration is null) {
      throw new InvalidOperationException("Hyper-V socket service is not registered.");
    }

    using var serverCertificate = identity.GetOrCreateServerCertificate();
    using var permits = new SemaphoreSlim(MAX_SESSIONS);
    var active = new ConcurrentDictionary<long, Task>();
    long nextSession = 0;
    var waitingForVm = false;

    try {
      while (!ct.IsCancellationRequested) {
        // wsl.exe is only started while a utility VM exists; an idle host costs no child processes.
        var snapshot = WslVmPresence.Snapshot();
        var vmId = snapshot.Length == 0 ? null : await TryGetPairedVmAsync(ct);
        if (vmId is null) {
          if (!waitingForVm) {
            VerboseLog.Write(VerboseEvent.BrokerWaitingForVm);
          }

          waitingForVm = true;
          await Task.Delay(snapshot.Length == 0 ? _pollInterval : _distributionInterval, ct);
          continue;
        }

        waitingForVm = false;
        using var listener = new Socket((AddressFamily)34, SocketType.Stream, (ProtocolType)1);
        listener.Bind(new HyperVEndpoint(vmId.Value));
        listener.Listen(MAX_SESSIONS);
        VerboseLog.Write(VerboseEvent.BrokerListening);
        await Console.Error.WriteLineAsync("Broker listening for a paired WSL client.");

        while (!ct.IsCancellationRequested) {
          await permits.WaitAsync(ct);
          try {
            using var acceptDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            acceptDeadline.CancelAfter(_pollInterval);
            Socket socket;

            try { socket = await listener.AcceptAsync(acceptDeadline.Token); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) {
              permits.Release();
              var current = WslVmPresence.Snapshot();
              if (current == snapshot) {
                continue;
              }

              // The set of VM processes changed: confirm the bound identifier before trusting it again.
              if (current.Length == 0 ||
                  await TryGetPairedVmAsync(ct) != vmId) {
                break;
              }

              snapshot = current;
              continue;
            }

            var id = Interlocked.Increment(ref nextSession);
            var session = HandleAsync(socket, serverCertificate, ct);
            active[id] = session;
            _ = session.ContinueWith(completed => {
              active.TryRemove(id, out var _);
              permits.Release();
              if (completed.IsFaulted) {
                Console.Error.WriteLine("Broker session failed.");
              }
            }, TaskScheduler.Default);
          }
          catch {
            permits.Release();
            throw;
          }
        }
      }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
    finally {
      using var grace = new CancellationTokenSource(TimeSpan.FromSeconds(30));

      try { await Task.WhenAll(active.Values).WaitAsync(grace.Token); }
      catch (OperationCanceledException) {
        /* Each session retains ownership until it ends. */
      }
    }
  }

  private async Task<Guid?> TryGetPairedVmAsync(CancellationToken ct) {
    foreach (var distribution in identity.PairedDistributions()) {
      var vmId = await VmIdProvider.TryGetAsync(distribution, ct);
      if (vmId is not null) {
        return vmId;
      }
    }

    return null;
  }

  private async Task HandleAsync(Socket socket, X509Certificate2 serverCertificate, CancellationToken ct) {
    using (socket) {
      await using var network = new NetworkStream(socket, false);
      using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
      deadline.CancelAfter(TimeSpan.FromSeconds(10));

      try {
        await using var authenticated = await BrokerTls.AuthenticateAsync(network, serverCertificate, identity,
          deadline.Token);
        VerboseLog.Write(VerboseEvent.BrokerPeerAuthenticated);
        await PinentryProtocol.RunAsync(authenticated.Stream, authenticated.Origin, true,
          new OriginLabelUi(ui, authenticated.Origin), cache, ct, productVersion);
      }
      catch (Exception error) when (error is AuthenticationException or IOException or SocketException or
                                      OperationCanceledException or InvalidDataException) {
        await Console.Error.WriteLineAsync($"Broker connection closed: {error.GetType().Name}.");
      }
    }
  }
}
